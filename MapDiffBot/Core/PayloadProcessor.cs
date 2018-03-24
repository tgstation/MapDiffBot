using Hangfire;
using MapDiffBot.Configuration;
using MapDiffBot.Controllers;
using MapDiffBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Z.EntityFramework.Plus;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
#pragma warning disable CA1812
	sealed class PayloadProcessor : IPayloadProcessor
#pragma warning restore CA1812
	{
		/// <summary>
		/// The URL to direct user to report issues at
		/// </summary>
		const string issueReportUrl = "https://github.com/MapDiffBot/MapDiffBot/issues";
		/// <summary>
		/// The intermediate directory for operations
		/// </summary>
		public const string WorkingDirectory = "MapDiffs";

		/// <summary>
		/// The <see cref="GitHubConfiguration"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly GitHubConfiguration gitHubConfiguration;
		/// <summary>
		/// The <see cref="GeneralConfiguration"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly GeneralConfiguration generalConfiguration;
		/// <summary>
		/// The <see cref="IGeneratorFactory"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly IGeneratorFactory generatorFactory;
		/// <summary>
		/// The <see cref="IGitHubManager"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly IServiceProvider serviceProvider;
		/// <summary>
		/// The <see cref="ILocalRepositoryManager"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly ILocalRepositoryManager repositoryManager;
		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly IIOManager ioManager;
		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly ILogger<PayloadProcessor> logger;
		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly IStringLocalizer<PayloadProcessor> stringLocalizer;
		/// <summary>
		/// The <see cref="IBackgroundJobClient"/> for the <see cref="PayloadProcessor"/>
		/// </summary>
		readonly IBackgroundJobClient backgroundJobClient;

		/// <summary>
		/// <see cref="Dictionary{TKey, TValue}"/> of operation name to their <see cref="CancellationToken"/>
		/// </summary>
		readonly Dictionary<string, CancellationTokenSource> mapDiffOperations;

		/// <summary>
		/// Construct a <see cref="PayloadProcessor"/>
		/// </summary>
		/// <param name="gitHubConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing the value of <see cref="gitHubConfiguration"/></param>
		/// <param name="generalConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing the value of <see cref="generalConfiguration"/></param>
		/// <param name="generatorFactory">The value of <see cref="generatorFactory"/></param>
		/// <param name="serviceProvider">The value of <see cref="serviceProvider"/></param>
		/// <param name="repositoryManager">The value of <see cref="repositoryManager"/></param>
		/// <param name="ioManager">The value of <see cref="ioManager"/></param>
		/// <param name="logger">The value of <see cref="logger"/></param>
		/// <param name="stringLocalizer">The value of <see cref="stringLocalizer"/></param>
		/// <param name="backgroundJobClient">The value of <see cref="backgroundJobClient"/></param>
		public PayloadProcessor(IOptions<GitHubConfiguration> gitHubConfigurationOptions, IOptions<GeneralConfiguration> generalConfigurationOptions, IGeneratorFactory generatorFactory, IServiceProvider serviceProvider, ILocalRepositoryManager repositoryManager, IIOManager ioManager, ILogger<PayloadProcessor> logger, IStringLocalizer<PayloadProcessor> stringLocalizer, IBackgroundJobClient backgroundJobClient)
		{
			gitHubConfiguration = gitHubConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(gitHubConfigurationOptions));
			generalConfiguration = generalConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(generalConfigurationOptions));
			this.ioManager = new ResolvingIOManager(ioManager ?? throw new ArgumentNullException(nameof(ioManager)), WorkingDirectory);
			this.generatorFactory = generatorFactory ?? throw new ArgumentNullException(nameof(generatorFactory));
			this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
			this.repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
			this.backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));

			mapDiffOperations = new Dictionary<string, CancellationTokenSource>();
		}

		/// <summary>
		/// Generates a map diff comment for the specified <see cref="PullRequest"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="PullRequest.Base"/> <see cref="Repository.Id"/></param>
		/// <param name="pullRequestNumber">The <see cref="PullRequest.Number"/></param>
		/// <param name="jobCancellationToken">The <see cref="IJobCancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		[AutomaticRetry(Attempts = 0)]
		public async Task ScanPullRequest(long repositoryId, int pullRequestNumber, IJobCancellationToken jobCancellationToken)
		{
			using(logger.BeginScope("Scanning pull request #{0} for repository {1}", pullRequestNumber, repositoryId))
			using (serviceProvider.CreateScope())
			{
				var serverCancellationToken = jobCancellationToken.ShutdownToken;
				var gitHubManager = serviceProvider.GetRequiredService<IGitHubManager>();
				var pullRequest = await gitHubManager.GetPullRequest(repositoryId, pullRequestNumber, serverCancellationToken).ConfigureAwait(false);

				logger.LogTrace("Repository is {0}/{1}", pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Name);
				logger.LogTrace("Pull Request: \"{0}\" by {1}", pullRequest.Title, pullRequest.User.Login);

				var changedMapsTask = gitHubManager.GetPullRequestChangedFiles(pullRequest, serverCancellationToken);
				var requestIdentifier = String.Concat(pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Name, pullRequest.Number);

				logger.LogTrace("Locking request identifier: {0}", requestIdentifier);

				//Generate our own cancellation token for rolling builds of the same PR
				using (var cts = new CancellationTokenSource())
				using (serverCancellationToken.Register(() => cts.Cancel()))
				{
					var cancellationToken = cts.Token;

					lock (mapDiffOperations)
					{
						if (mapDiffOperations.TryGetValue(requestIdentifier, out CancellationTokenSource oldOperation))
						{
							logger.LogDebug("Cancelling previous scan on request identifier: {0}", requestIdentifier);
							oldOperation.Cancel();
							mapDiffOperations[requestIdentifier] = cts;
						}
						else
							mapDiffOperations.Add(requestIdentifier, cts);
					}

					try
					{
						for (var I = 0; !pullRequest.Mergeable.HasValue && I < 5; cancellationToken.ThrowIfCancellationRequested(), cancellationToken.ThrowIfCancellationRequested(), ++I)
						{
							if(I == 0)
								logger.LogTrace("Null mergable state on pull request, refreshing for a maximum of 10s");
							await Task.Delay(1000 * I, cancellationToken).ConfigureAwait(false);
							pullRequest = await gitHubManager.GetPullRequest(pullRequest.Base.Repository.Id, pullRequest.Number, cancellationToken).ConfigureAwait(false); ;
						}

						if (!pullRequest.Mergeable.HasValue || !pullRequest.Mergeable.Value)
						{
							logger.LogDebug("Pull request unmergeable, aborting scan");
							return;
						}

						var allChangedMaps = await changedMapsTask.ConfigureAwait(false);
						var changedDmms = allChangedMaps.Where(x => x.FileName.EndsWith(".dmm", StringComparison.InvariantCultureIgnoreCase)).Select(x => x.FileName).ToList();
						if (changedDmms.Count == 0)
						{
							logger.LogDebug("Pull request has no changed maps, exiting");
							return;
						}

						await GenerateDiffs(pullRequest, changedDmms, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						logger.LogTrace("Operation cancelled");
						throw;
					}
					catch (Exception e)
					{
						logger.LogDebug(e, "Error occurred. Attempting to post debug comment");
						try
						{
							//if this is the only exception, throw it directly, otherwise pile it in the exception collection
							await gitHubManager.CreateSingletonComment(pullRequest, stringLocalizer["An error occurred during the operation:\n\n```\n{0}\n\n```\n\nPlease report this issue [here]({1}).", e, issueReportUrl], cancellationToken).ConfigureAwait(false);
							throw;
						}
						catch (OperationCanceledException)
						{
							logger.LogTrace("Operation cancelled");
						}
						catch (Exception innerException)
						{
							throw new AggregateException(innerException, e);
						}
						finally
						{
							cts.Cancel();
						}
					}
					finally
					{
						logger.LogTrace("Unlocking request identifier {0}", requestIdentifier);
						lock (mapDiffOperations)
							if (mapDiffOperations.TryGetValue(requestIdentifier, out CancellationTokenSource maybeOurOperation) && maybeOurOperation == cts)
								mapDiffOperations.Remove(requestIdentifier);
					}
				}
			}
		}

		/// <summary>
		/// Generate map diffs for a given <paramref name="pullRequest"/>
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/></param>
		/// <param name="changedDmms">Paths to changed .dmm files</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		async Task GenerateDiffs(PullRequest pullRequest, IReadOnlyList<string> changedDmms, CancellationToken cancellationToken)
		{
			using (logger.BeginScope("Generating {0} diffs for pull request #{1} in {2}/{3}", changedDmms.Count, pullRequest.Number, pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Name))
			{
				const string OldMapExtension = ".old_map_diff_bot";

				var gitHubManager = serviceProvider.GetRequiredService<IGitHubManager>();

				Task generatingCommentTask;
				List<Task<RenderResult>> afterRenderings, beforeRenderings;

				var workingDir = ioManager.ConcatPath(pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Name, pullRequest.Number.ToString(CultureInfo.InvariantCulture));
				logger.LogTrace("Setting workdir to {0}", workingDir);
				IIOManager currentIOManager = new ResolvingIOManager(ioManager, workingDir);
				string repoPath;

				int lastProgress = -1;
				Task lastProgressUpdate = Task.CompletedTask;
				async Task OnCloneProgress(int progress)
				{
					lock (gitHubManager)
					{
						if (lastProgress >= progress)
							return;
						if (lastProgress == -1)
							logger.LogInformation("Waiting on repository to finish cloning...");
						lastProgress = progress;
					}
					await lastProgressUpdate.ConfigureAwait(false);
					await gitHubManager.CreateSingletonComment(pullRequest, stringLocalizer["Cloning repository... ({0}%)", progress], cancellationToken).ConfigureAwait(false);
				};
				Task CreateBlockedComment()
				{
					logger.LogInformation("Waiting for another diff generation on {0}/{1} to complete...", pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Name);
					return gitHubManager.CreateSingletonComment(pullRequest, stringLocalizer["Waiting for another operation on this repository to complete..."], cancellationToken);
				};

				logger.LogTrace("Locking repository...");
				using (var repo = await repositoryManager.GetRepository(pullRequest.Base.Repository, OnCloneProgress, CreateBlockedComment, cancellationToken).ConfigureAwait(false))
				{
					logger.LogTrace("Repository ready");
					generatingCommentTask = gitHubManager.CreateSingletonComment(pullRequest, stringLocalizer["Generating diffs..."], cancellationToken);
					//prep the outputDirectory
					async Task DirectoryPrep()
					{
						logger.LogTrace("Cleaning workdir...");
						await currentIOManager.DeleteDirectory(".", cancellationToken).ConfigureAwait(false);
						await currentIOManager.CreateDirectory(".", cancellationToken).ConfigureAwait(false);
						logger.LogTrace("Workdir cleaned");
					};

					var dirPrepTask = DirectoryPrep();
					//get the dme to use
					var dmeToUseTask = serviceProvider.GetRequiredService<IDatabaseContext>().InstallationRepositories.Where(x => x.Id == pullRequest.Base.Repository.Id).Select(x => x.TargetDme).ToAsyncEnumerable().FirstOrDefault(cancellationToken);

					var oldMapPaths = new List<string>()
					{
						Capacity = changedDmms.Count
					};
					try
					{
						//fetch base commit if necessary and check it out, fetch pull request
						if (!await repo.ContainsCommit(pullRequest.Base.Sha, cancellationToken).ConfigureAwait(false))
						{
							logger.LogTrace("Base commit not found, running fetch...");
							await repo.Fetch(cancellationToken).ConfigureAwait(false);
						}
						logger.LogTrace("Moving HEAD to pull request base...");
						await repo.Checkout(pullRequest.Base.Sha, cancellationToken).ConfigureAwait(false);

						//but since we don't need this right await don't await it yet
						var pullRequestFetchTask = repo.FetchPullRequest(pullRequest.Number, cancellationToken);
						try
						{
							//first copy all modified maps to the same location with the .old_map_diff_bot extension
							async Task<string> CacheMap(string mapPath)
							{
								var originalPath = currentIOManager.ConcatPath(repoPath, mapPath);
								if (await currentIOManager.FileExists(originalPath, cancellationToken).ConfigureAwait(false))
								{
									logger.LogTrace("Creating old map cache of {0}", mapPath);
									var oldMapPath = String.Format(CultureInfo.InvariantCulture, "{0}{1}", originalPath, OldMapExtension);
									await currentIOManager.CopyFile(originalPath, oldMapPath, cancellationToken).ConfigureAwait(false);
									return oldMapPath;
								}
								return null;
							};

							repoPath = repo.Path;

							var tasks = changedDmms.Select(x => CacheMap(x)).ToList();
							await Task.WhenAll(tasks).ConfigureAwait(false);
							oldMapPaths.AddRange(tasks.Select(x => x.Result));
						}
						finally
						{
							logger.LogTrace("Waiting for pull request commits to be available...");
							await pullRequestFetchTask.ConfigureAwait(false);
						}

						logger.LogTrace("Creating and moving HEAD to pull request merge commit...");
						//generate the merge commit ourselves since we can't get it from GitHub because itll return an outdated one
						await repo.Merge(pullRequest.Head.Sha, cancellationToken).ConfigureAwait(false);
					}
					finally
					{
						logger.LogTrace("Waiting for configured project dme...");
						await dmeToUseTask.ConfigureAwait(false);
					}

					//create empty array of map regions
					var mapRegions = Enumerable.Repeat<MapRegion>(null, changedDmms.Count).ToList();
					var dmeToUse = dmeToUseTask.Result;

					var generator = generatorFactory.CreateGenerator(dmeToUse, new ResolvingIOManager(ioManager, repoPath));
					var outputDirectory = currentIOManager.ResolvePath(".");
					logger.LogTrace("Full workdir path: {0}", outputDirectory);
					//Generate MapRegions for modified maps and render all new maps
					async Task<RenderResult> DiffAndRenderNewMap(int I)
					{
						await dirPrepTask.ConfigureAwait(false);
						var originalPath = currentIOManager.ConcatPath(repoPath, changedDmms[I]);
						if (!await currentIOManager.FileExists(originalPath, cancellationToken).ConfigureAwait(false))
						{
							logger.LogTrace("No new map for path {0} exists, skipping region detection and after render", changedDmms[I]);
							return new RenderResult { InputPath = changedDmms[I], ToolOutput = stringLocalizer["Map missing!"] };
						}
						ToolResult result = null;
						if (oldMapPaths[I] != null)
						{
							logger.LogTrace("Getting diff region for {0}...", changedDmms[I]);
							result = await generator.GetDifferences(oldMapPaths[I], originalPath, cancellationToken).ConfigureAwait(false);
							var region = result.MapRegion;
							logger.LogTrace("Diff region for {0}: {1}", changedDmms[I], region);
							if (region != null)
							{
								var xdiam = region.MaxX - region.MinX;
								var ydiam = region.MaxY - region.MinY;
								const int minDiffDimensions = 5 - 1;
								if (xdiam < minDiffDimensions || ydiam < minDiffDimensions)
								{
									//need to expand
									var fullResult = await generator.GetMapSize(originalPath, cancellationToken).ConfigureAwait(false);
									var fullRegion = fullResult.MapRegion;
									if (fullRegion == null)
									{
										//give up
										region = null;
									}
									else
									{
										bool increaseMax = true;
										if (xdiam < minDiffDimensions && ((fullRegion.MaxX - fullRegion.MinX) >= minDiffDimensions))
											while ((region.MaxX - region.MinX) < minDiffDimensions)
											{
												if (increaseMax)
													region.MaxX = (short)Math.Min(region.MaxX + 1, fullRegion.MaxX);
												else
													region.MinX = (short)Math.Max(region.MinX - 1, 1);
												increaseMax = !increaseMax;
											}
										if (ydiam < minDiffDimensions && ((fullRegion.MaxY - fullRegion.MinY) >= minDiffDimensions))
											while ((region.MaxY - region.MinY) < minDiffDimensions)
											{
												if (increaseMax)
													region.MaxY = (short)Math.Min(region.MaxY + 1, fullRegion.MaxY);
												else
													region.MinY = (short)Math.Max(region.MinY - 1, 1);
												increaseMax = !increaseMax;
											}
									}
									logger.LogTrace("Region for {0} expanded to {1}", region);
								}
								mapRegions[I] = region;
							}
						}
						else
							logger.LogTrace("Skipping region detection for {0} due to old map not existing", changedDmms[I]);
						logger.LogTrace("Performing after rendering for {0}...", changedDmms[I]);
						var renderResult = await generator.RenderMap(originalPath, mapRegions[I], outputDirectory, "after", cancellationToken).ConfigureAwait(false);
						logger.LogTrace("After rendering for {0} complete! Result path: {1}, Output: {2}", changedDmms[I], renderResult.OutputPath, renderResult.ToolOutput);
						if (result != null)
							renderResult.ToolOutput = String.Format(CultureInfo.InvariantCulture, "Differences task:{0}{1}{0}Render task:{0}{2}", Environment.NewLine, result.ToolOutput, renderResult.ToolOutput);
						return renderResult;
					};

					logger.LogTrace("Running iterations of DiffAndRenderNewMap...");
					//finish up before we go back to the base branch
					afterRenderings = Enumerable.Range(0, changedDmms.Count).Select(I => DiffAndRenderNewMap(I)).ToList();
					try
					{
						await Task.WhenAll(afterRenderings).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						logger.LogDebug(e, "After renderings produced exception!");
						//at this point everything is done but some have failed
						//we'll handle it later
					}

					logger.LogTrace("Moving HEAD back to pull request base...");
					await repo.Checkout(pullRequest.Base.Sha, cancellationToken).ConfigureAwait(false);

					Task<RenderResult> RenderOldMap(int i)
					{
						var oldPath = oldMapPaths[i];
						if (oldMapPaths != null)
						{
							logger.LogTrace("Performing before rendering for {0}...", changedDmms[i]);
							return generator.RenderMap(oldPath, mapRegions[i], outputDirectory, "before", cancellationToken);
						}
						return Task.FromResult(new RenderResult { InputPath = changedDmms[i], ToolOutput = stringLocalizer["Map missing!"] });
					}

					logger.LogTrace("Running iterations of RenderOldMap...");
					//finish up rendering
					beforeRenderings = Enumerable.Range(0, changedDmms.Count).Select(I => RenderOldMap(I)).ToList();
					try
					{
						await Task.WhenAll(beforeRenderings).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						logger.LogDebug(e, "Before renderings produced exception!");
						//see above
					}

					//done with the repo at this point
					logger.LogTrace("Renderings complete. Releasing reposiotory");
				}

				//collect results and errors
				async Task<KeyValuePair<MapDiff, MapRegion>> GetResult(int i)
				{
					var beforeTask = beforeRenderings[i];
					var afterTask = afterRenderings[i];

					var result = new MapDiff
					{
						InstallationRepositoryId = pullRequest.Base.Repository.Id,
						PullRequestNumber = pullRequest.Number,
						FileId = i,
					};

					RenderResult GetRenderingResult(Task<RenderResult> task)
					{
						if (task.Exception != null)
						{
							result.LogMessage = result.LogMessage == null ? task.Exception.ToString() : String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}", result.LogMessage, Environment.NewLine, task.Exception);
							return null;
						}
						return task.Result;
					};

					var r1 = GetRenderingResult(beforeTask);
					var r2 = GetRenderingResult(afterTask);

					logger.LogTrace("Results for {0}: Before {1}, After {2}", changedDmms[i], r1?.OutputPath ?? "NONE", r2?.OutputPath ?? "NONE");

					result.MapPath = changedDmms[i];

					result.LogMessage = String.Format(CultureInfo.InvariantCulture, "Job {5}:{0}Path: {6}{0}Before:{0}Command Line: {1}{0}Output:{0}{2}{0}Logs:{0}{7}{0}After:{0}Command Line: {3}{0}Output:{0}{4}{0}Logs:{0}{8}{0}Exceptions:{0}{9}{0}", Environment.NewLine, r1?.CommandLine, r1?.OutputPath, r2?.CommandLine, r2?.OutputPath, i + 1, result.MapPath, r1?.ToolOutput, r2?.ToolOutput, result.LogMessage);

					async Task<byte[]> ReadMapImage(string path)
					{
						if (path != null && await currentIOManager.FileExists(path, cancellationToken).ConfigureAwait(false))
						{
							var bytes = await currentIOManager.ReadAllBytes(path, cancellationToken).ConfigureAwait(false);
							await currentIOManager.DeleteFile(path, cancellationToken).ConfigureAwait(false);
							return bytes;
						}
						return null;
					}

					var readBeforeTask = ReadMapImage(r1?.OutputPath);
					result.AfterImage = await ReadMapImage(r2?.OutputPath).ConfigureAwait(false);
					result.BeforeImage = await readBeforeTask.ConfigureAwait(false);

					return new KeyValuePair<MapDiff, MapRegion>(result, r2?.MapRegion);
				}

				logger.LogTrace("Waiting for notification comment to POST...");
				await generatingCommentTask.ConfigureAwait(false);

				logger.LogTrace("Collecting results...");
				var results = Enumerable.Range(0, changedDmms.Count).Select(x => GetResult(x)).ToList();
				await Task.WhenAll(results).ConfigureAwait(false);
				var dic = new Dictionary<MapDiff, MapRegion>();
				foreach (var I in results.Select(x => x.Result))
					dic.Add(I.Key, I.Value);
				await HandleResults(pullRequest, dic, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Publish a <see cref="List{T}"/> of <paramref name="diffResults"/>s to the <see cref="IDatabaseContext"/> and GitHub
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/> the <paramref name="diffResults"/> are for</param>
		/// <param name="diffResults">The map of <see cref="MapDiff"/>s to <see cref="MapRegion"/>s</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		async Task HandleResults(PullRequest pullRequest, Dictionary<MapDiff, MapRegion> diffResults, CancellationToken cancellationToken)
		{
			int formatterCount = 0;
			
			var databaseContext = serviceProvider.GetRequiredService<IDatabaseContext>();
			
			var deleteTask = databaseContext.MapDiffs.Where(x => x.InstallationRepositoryId == pullRequest.Base.Repository.Id && x.PullRequestNumber == pullRequest.Number).DeleteAsync(cancellationToken);

			var commentBuilder = new StringBuilder();
			var prefix = generalConfiguration.ApplicationPrefix;
			logger.LogTrace("Generating comment and preparing database query...");
			foreach (var kv in diffResults)
			{
				var I = kv.Key;
				var beforeUrl = String.Concat(prefix, FilesController.RouteTo(pullRequest, formatterCount, "before"));
				var afterUrl = String.Concat(prefix, FilesController.RouteTo(pullRequest, formatterCount, "after"));
				var logsUrl = String.Concat(prefix, FilesController.RouteTo(pullRequest, formatterCount, "logs"));

				commentBuilder.Append(String.Format(CultureInfo.InvariantCulture,
					"<details><summary>{0}</summary>{11}{11}{1} | {2}{11}--- | ---{11}![]({3}) | ![]({4}){11}{11}{5} | {6} | {7} | {12}{11}--- | --- | --- | ---{11}{8} | {9} | [{7}]({10}) | [{1}]({3}) \\| [{2}]({4}){11}{11}</details>{11}{11}",
					I.MapPath,
					stringLocalizer["Old"],
					stringLocalizer["New"],
					beforeUrl,
					afterUrl,
					stringLocalizer["Status"],
					stringLocalizer["Region"],
					stringLocalizer["Logs"],
					I.BeforeImage != null ? (I.AfterImage != null ? stringLocalizer["Modified"] : stringLocalizer["Deleted"]) : stringLocalizer["Created"],
					kv.Value?.ToString() ?? stringLocalizer["ALL"],
					logsUrl,
					Environment.NewLine,
					stringLocalizer["Raw"]
					));
				logger.LogTrace("Adding MapDiff for {0}...", I.MapPath);
				databaseContext.MapDiffs.Add(I);
				++formatterCount;
			}
			
			var comment = String.Format(CultureInfo.CurrentCulture,
				"{4}<br>{0}{3}{3}{3}{3}<br>{1}{3}{3}{3}{3}{2}{3}{3}{3}{3}{5}", 
				commentBuilder,
				stringLocalizer["Last updated from merging commit {0} into {1}", pullRequest.Head.Sha, pullRequest.Base.Sha],
				stringLocalizer["Full job logs available [here]({0})", String.Concat(prefix, FilesController.RouteToBrowse(pullRequest))],
				Environment.NewLine,
				stringLocalizer["Maps with diff:"],
				stringLocalizer["Please report any issues [here]({0}).", issueReportUrl]
				);

			logger.LogTrace("Waiting for old MapDiffs to delete...");
			await deleteTask.ConfigureAwait(false);
			logger.LogTrace("Committing new MapDiffs to the database...");
			await databaseContext.Save(cancellationToken).ConfigureAwait(false);
			logger.LogTrace("Creating MapDiffBot comment...");
			await serviceProvider.GetRequiredService<IGitHubManager>().CreateSingletonComment(pullRequest, comment, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public void ProcessPayload(PullRequestEventPayload payload)
		{
			if (payload.Action == "opened" || payload.Action == "synchronize")
				backgroundJobClient.Enqueue(() => ScanPullRequest(payload.Repository.Id, payload.PullRequest.Number, JobCancellationToken.Null));
		}

		/// <inheritdoc />
		public void ProcessPayload(IssueCommentPayload payload)
		{
			if (payload.Action != "created" || payload.Comment.Body == null || !payload.Comment.Body.Split(' ').Any(x => x == String.Format(CultureInfo.InvariantCulture, "@{0}", gitHubConfiguration.TagUser)))
				return;
			backgroundJobClient.Enqueue(() => ScanPullRequest(payload.Repository.Id, payload.Issue.Number, JobCancellationToken.Null));
		}
	}
}
