using Hangfire;
using MapDiffBot.Configuration;
using MapDiffBot.Controllers;
using MapDiffBot.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
		}

		/// <summary>
		/// Generates a map diff comment for the specified <see cref="PullRequest"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="PullRequest.Base"/> <see cref="Repository.Id"/></param>
		/// <param name="pullRequestNumber">The <see cref="PullRequest.Number"/></param>
		/// <param name="jobCancellationToken">The <see cref="IJobCancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		[AutomaticRetry(Attempts = 0)]
		public async Task ScanCommit(Repository repository, string branch, string sha, string parentSha, long checkSuiteId, IJobCancellationToken jobCancellationToken)
		{
			using (logger.BeginScope("Scanning sha {0} for repository {1}", sha, repository.FullName))
			using (serviceProvider.CreateScope())
			{
				var cancellationToken = jobCancellationToken.ShutdownToken;
				var gitHubManager = serviceProvider.GetRequiredService<IGitHubManager>();

				var changedMapsTask = gitHubManager.GetCommitChangedFiles(repository, sha, cancellationToken);
				var requestIdentifier = String.Concat(repository.Id, '-', checkSuiteId);

				logger.LogTrace("Locking request identifier: {0}", requestIdentifier);
				long? checkRunId = null;
				try
				{
					var allChangedMaps = await changedMapsTask.ConfigureAwait(false);
					var changedDmms = allChangedMaps.Where(x => x.EndsWith(".dmm", StringComparison.InvariantCultureIgnoreCase)).ToList();
					if (changedDmms.Count == 0)
					{
						logger.LogDebug("Commit has no changed maps, exiting");
						return;
					}

					var initializer = new NewCheckRun
					{
						StartedAt = DateTimeOffset.Now,
						Conclusion = CheckConclusion.Neutral,
						Name = "Map Renderings",
						Status = CheckStatus.Queued,
						HeadSha = sha,
						HeadBranch = branch,
					};
					checkRunId = await gitHubManager.CreateCheckRun(repository, checkSuiteId, initializer, cancellationToken).ConfigureAwait(false);

					await GenerateDiffs(repository, branch, sha, parentSha, checkRunId.Value, changedDmms, cancellationToken).ConfigureAwait(false);
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
						if (checkRunId.HasValue)
							await gitHubManager.UpdateCheckRun(repository, checkRunId.Value, new CheckRunUpdate
							{
								CompletedAt = DateTimeOffset.Now,
								Status = CheckStatus.Completed,
								Output = new CheckRunOutput("Error Output", e.ToString(), null, null)
							}, default).ConfigureAwait(false);
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
		async Task GenerateDiffs(Repository repository, string branch, string sha, string parentSha, long checkRunId, IReadOnlyList<string> changedDmms, CancellationToken cancellationToken)
		{
			using (logger.BeginScope("Generating {0} diffs for repository {1} check run {2}", changedDmms.Count, repository.FullName, checkRunId))
			{
				const string OldMapExtension = ".old_map_diff_bot";

				var gitHubManager = serviceProvider.GetRequiredService<IGitHubManager>();

				Task generatingCommentTask;
				List<Task<RenderResult>> afterRenderings, beforeRenderings;

				var workingDir = ioManager.ConcatPath(repository.Id.ToString(CultureInfo.InvariantCulture), checkRunId.ToString(CultureInfo.InvariantCulture));
				logger.LogTrace("Setting workdir to {0}", workingDir);
				IIOManager currentIOManager = new ResolvingIOManager(ioManager, workingDir);
				string repoPath;

				int lastProgress = -1;
				Task lastProgressUpdate = Task.CompletedTask;

				bool inProgress = false;
				async Task SetInProgress() {
					if (inProgress)
						return;
					inProgress = true;
					await gitHubManager.UpdateCheckRun(repository, checkRunId, new CheckRunUpdate
					{
						Status = CheckStatus.InProgress
					}, cancellationToken).ConfigureAwait(false);
				};

				async Task OnCloneProgress(int progress)
				{
					bool setInProgress;
					lock (gitHubManager)
					{
						if (lastProgress >= progress)
							return;
						setInProgress = lastProgress == -1;
						if(setInProgress)
							logger.LogInformation("Waiting on repository to finish cloning...");
						lastProgress = progress;
					}
					if (setInProgress)
						await SetInProgress().ConfigureAwait(false);
					await lastProgressUpdate.ConfigureAwait(false);
				};
				logger.LogTrace("Locking repository...");
				using (var repo = await repositoryManager.GetRepository(repository, OnCloneProgress, () => Task.CompletedTask, cancellationToken).ConfigureAwait(false))
				{
					logger.LogTrace("Repository ready");
					generatingCommentTask = SetInProgress();
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
					var dmeToUseTask = serviceProvider.GetRequiredService<IDatabaseContext>().InstallationRepositories.Where(x => x.Id == repository.Id).Select(x => x.TargetDme).ToAsyncEnumerable().FirstOrDefault(cancellationToken);

					var oldMapPaths = new List<string>()
					{
						Capacity = changedDmms.Count
					};
					try
					{
						//fetch base commit if necessary and check it out, fetch pull request
						if (!await repo.ContainsCommit(sha, cancellationToken).ConfigureAwait(false))
						{
							logger.LogTrace("Commit not found, running fetch...");
							await repo.Fetch(branch, cancellationToken).ConfigureAwait(false);
						}
						logger.LogTrace("Moving HEAD to parent commit...");
						await repo.Checkout(parentSha, cancellationToken).ConfigureAwait(false);

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

						logger.LogTrace("Moving HEAD to target commit...");
						await repo.Checkout(sha, cancellationToken).ConfigureAwait(false);
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
									logger.LogTrace("Region for {0} expanded to {1}", changedDmms[I], region);
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

					logger.LogTrace("Moving HEAD back to parent commit...");
					await repo.Checkout(parentSha, cancellationToken).ConfigureAwait(false);

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
						InstallationRepositoryId = repository.Id,
						CheckRunId = checkRunId,
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
				await HandleResults(repository, checkRunId, dic, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Publish a <see cref="List{T}"/> of <paramref name="diffResults"/>s to the <see cref="IDatabaseContext"/> and GitHub
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/> the <paramref name="diffResults"/> are for</param>
		/// <param name="diffResults">The map of <see cref="MapDiff"/>s to <see cref="MapRegion"/>s</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		async Task HandleResults(Repository repository, long checkRunId, Dictionary<MapDiff, MapRegion> diffResults, CancellationToken cancellationToken)
		{
			int formatterCount = 0;

			var databaseContext = serviceProvider.GetRequiredService<IDatabaseContext>();

			var deleteTask = databaseContext.MapDiffs.Where(x => x.InstallationRepositoryId == repository.Id && x.CheckRunId == checkRunId).DeleteAsync(cancellationToken);
			
			var prefix = generalConfiguration.ApplicationPrefix;
			logger.LogTrace("Generating comment and preparing database query...");
			var outputImages = new List<CheckRunImage>()
			{
				Capacity = diffResults.Count
			};
			foreach (var kv in diffResults)
			{
				var I = kv.Key;
				var beforeUrl = String.Concat(prefix, FilesController.RouteTo(repository, checkRunId, formatterCount, "before"));
				var afterUrl = String.Concat(prefix, FilesController.RouteTo(repository, checkRunId, formatterCount, "after"));
				var logsUrl = String.Concat(prefix, FilesController.RouteTo(repository, checkRunId, formatterCount, "logs"));
				
				logger.LogTrace("Adding MapDiff for {0}...", I.MapPath);
				var region = kv.Value?.ToString() ?? stringLocalizer["ALL"];
				outputImages.Add(new CheckRunImage(region, beforeUrl, stringLocalizer["Old"]));
				outputImages.Add(new CheckRunImage(region, afterUrl, stringLocalizer["New"]));
				databaseContext.MapDiffs.Add(I);
				++formatterCount;
			}

			logger.LogTrace("Waiting for old MapDiffs to delete...");
			await deleteTask.ConfigureAwait(false);
			logger.LogTrace("Committing new MapDiffs to the database...");
			await databaseContext.Save(cancellationToken).ConfigureAwait(false);
			logger.LogTrace("Finalizing GitHub Check...");
			
			var ncr = new CheckRunUpdate
			{
				DetailsUrl = FilesController.RouteToBrowse(repository, checkRunId),
				Status = CheckStatus.Completed,
				CompletedAt = DateTimeOffset.Now,
				Output = new CheckRunOutput("Map Renderings", null, null, outputImages)
			};
			await serviceProvider.GetRequiredService<IGitHubManager>().UpdateCheckRun(repository, checkRunId, ncr, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public void ProcessCheckSuite(CheckSuite checkSuite, bool rerequest) => backgroundJobClient.Enqueue(() => ScanCommit(checkSuite.Repository, checkSuite.HeadBranch, checkSuite.HeadSha, checkSuite.Before, checkSuite.Id, JobCancellationToken.Null));
	}
}
