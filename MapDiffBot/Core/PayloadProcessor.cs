using Hangfire;
using MapDiffBot.Controllers;
using MapDiffBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
    sealed class PayloadProcessor : IPayloadProcessor
	{
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
		
		public PayloadProcessor(IGeneratorFactory generatorFactory, IServiceProvider serviceProvider, ILocalRepositoryManager repositoryManager, IIOManager ioManager, ILogger<PayloadProcessor> logger, IStringLocalizer<PayloadProcessor> stringLocalizer, IBackgroundJobClient backgroundJobClient)
		{
			this.ioManager = new ResolvingIOManager(ioManager ?? throw new ArgumentNullException(nameof(ioManager)), "MapDiffs");
			this.generatorFactory = generatorFactory ?? throw new ArgumentNullException(nameof(generatorFactory));
			this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
			this.repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
			this.ioManager = ioManager ?? throw new ArgumentNullException(nameof(ioManager));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
			this.backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));

			mapDiffOperations = new Dictionary<string, CancellationTokenSource>();
		}

		/// <summary>
		/// Generates a map diff comment for the specified <see cref="PullRequest"/>
		/// </summary>
		/// <param name="pullRequestNumber">The <see cref="PullRequest.Number"/></param>
		/// <param name="pullRequestNumber">The <see cref="PullRequest.Base"/> <see cref="Repository.Id"/></param>
		/// <param name="baseUrl">The URL to use as a link base</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		public async Task GenerateMapDiff(int pullRequestNumber, long repositoryId, string baseUrl, IJobCancellationToken jobCancellationToken)
		{
			using (serviceProvider.CreateScope())
			{
				var gitHubManager = serviceProvider.GetRequiredService<IGitHubManager>();
				var pullRequest = await gitHubManager.GetPullRequest(repositoryId, pullRequestNumber, jobCancellationToken.ShutdownToken).ConfigureAwait(false);
				
				var changedMapsTask = gitHubManager.GetPullRequestChangedFiles(pullRequest, jobCancellationToken.ShutdownToken);
				var requestIdentifier = String.Concat(pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Name, pullRequest.Number);
				IIOManager currentIOManager = new ResolvingIOManager(ioManager, requestIdentifier);

				//Generate our own cancellation token for rolling builds of the same PR
				using (var cts = new CancellationTokenSource())
				using (jobCancellationToken.ShutdownToken.Register(() => cts.Cancel()))
				{
					var cancellationToken = cts.Token;

					lock (mapDiffOperations)
					{
						if (mapDiffOperations.TryGetValue(requestIdentifier, out CancellationTokenSource oldOperation))
						{
							oldOperation.Cancel();
							mapDiffOperations[requestIdentifier] = cts;
						}
						else
							mapDiffOperations.Add(requestIdentifier, cts);
					}

					var errors = new List<Exception>();
					try
					{
						for (var I = 0; !pullRequest.Mergeable.HasValue && I < 5; cancellationToken.ThrowIfCancellationRequested(), cancellationToken.ThrowIfCancellationRequested(), ++I)
						{
							await Task.Delay(1000 * I, cancellationToken).ConfigureAwait(false);
							pullRequest = await gitHubManager.GetPullRequest(repositoryId, pullRequestNumber, cancellationToken).ConfigureAwait(false); ;
						}

						if (!pullRequest.Mergeable.HasValue || !pullRequest.Mergeable.Value)
							return;

						var allChangedMaps = await changedMapsTask.ConfigureAwait(false);
						var changedDmms = allChangedMaps.Where(x => x.FileName.EndsWith(".dmm", StringComparison.InvariantCultureIgnoreCase)).Select(x => x.FileName).ToList();
						if (changedDmms.Count == 0)
							return;

						Task CreateCloneComment() => gitHubManager.CreateSingletonComment(pullRequest, stringLocalizer["OnClone"], cancellationToken);

						Task generatingCommentTask;
						List<Task<RenderResult>> beforeRenderings, afterRenderings;
						using (var repo = await repositoryManager.GetRepository(pullRequest.Base.Repository, CreateCloneComment, cancellationToken).ConfigureAwait(false))
						{
							generatingCommentTask = gitHubManager.CreateSingletonComment(pullRequest, stringLocalizer["OnGenerating"], cancellationToken);
							//prep the outputDirectory
							async Task DirectoryPrep()
							{
								await currentIOManager.DeleteDirectory(".", cancellationToken).ConfigureAwait(false);
								await currentIOManager.CreateDirectory(".", cancellationToken).ConfigureAwait(false);
							};

							async Task<string> FindDME()
							{
								var dmes = await currentIOManager.GetFilesWithExtension(repo.Path, "dme", cancellationToken).ConfigureAwait(false);
								var lowerRepo = pullRequest.Base.Repository.Name.ToUpperInvariant();
								foreach (var I in dmes)
									if (I.ToUpperInvariant().Contains(lowerRepo))
										return I;
								//meh
								return dmes.First();
							};

							//fetch base commit if necessary and check it out, fetch pull request
							var dirPrepTask = DirectoryPrep();
							var dmeToUseTask = FindDME();

							if (!await repo.ContainsCommit(pullRequest.Base.Sha, cancellationToken).ConfigureAwait(false))
								await repo.Fetch(cancellationToken).ConfigureAwait(false);
							await repo.Checkout(pullRequest.Base.Sha, cancellationToken).ConfigureAwait(false);

							var pullRequestFetchTask = repo.FetchPullRequest(pullRequest.Number, cancellationToken);

							var oldMapPaths = new List<string>()
							{
								Capacity = changedDmms.Count
							};

							//first copy all modified maps to the same location with the .old_map_diff_bot extension
							async Task<string> CacheMap(string mapPath)
							{
								var originalPath = currentIOManager.ConcatPath(repo.Path, mapPath);
								if (await currentIOManager.FileExists(originalPath, cancellationToken).ConfigureAwait(false))
								{
									var oldMapPath = String.Format(CultureInfo.InvariantCulture, "{0}.old_map_diff_bot", originalPath);
									await currentIOManager.CopyFile(originalPath, oldMapPath, cancellationToken).ConfigureAwait(false);
									return oldMapPath;
								}
								return null;
							};

							await Task.WhenAll(changedDmms.Select(x => CacheMap(x))).ConfigureAwait(false);

							//generate the merge commit ourselves since we can't get it from GitHub because itll return an outdated one
							await pullRequestFetchTask.ConfigureAwait(false);
							await repo.Merge(pullRequest.Head.Sha, cancellationToken).ConfigureAwait(false);

							var mapRegions = Enumerable.Repeat<MapRegion>(null, changedDmms.Count).ToList();
							var dmeToUse = await dmeToUseTask.ConfigureAwait(false);

							var generator = generatorFactory.CreateGenerator(dmeToUse, new ResolvingIOManager(ioManager, repo.Path));

							var outputDirectory = ioManager.ResolvePath(".");

							//Generate MapRegions for modified maps and render all new maps
							async Task<RenderResult> DiffAndRenderNewMap(int I)
							{
								var originalPath = currentIOManager.ConcatPath(repo.Path, changedDmms[I]);
								if (!await currentIOManager.FileExists(originalPath, cancellationToken).ConfigureAwait(false))
									return new RenderResult { ToolOutput = stringLocalizer["Map missing!"] };
								if (oldMapPaths[I] != null)
								{
									var result = await generator.GetDifferences(oldMapPaths[I], originalPath, cancellationToken).ConfigureAwait(false);
									var region = result.MapRegion;
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
										mapRegions[I] = region;
									}
								}
								return await generator.RenderMap(originalPath, mapRegions[I], outputDirectory, "after", cancellationToken).ConfigureAwait(false);
							};

							//finish up before we go back to the base branch
							beforeRenderings = Enumerable.Range(0, changedDmms.Count).Select(I => DiffAndRenderNewMap(I)).ToList();
							try
							{
								await Task.WhenAll(beforeRenderings).ConfigureAwait(false);
							}
							catch (Exception)
							{
								//at this point everything is done but some have failed
								//we'll handle it later
							}
							await repo.Checkout(pullRequest.Base.Sha, cancellationToken).ConfigureAwait(false);

							Task<RenderResult> RenderOldMap(int i)
							{
								var oldPath = oldMapPaths[i];
								if (oldMapPaths != null)
									return generator.RenderMap(oldPath, mapRegions[i], outputDirectory, "before", cancellationToken);
								return Task.FromResult(new RenderResult { ToolOutput = stringLocalizer["Map missing!"] });
							}

							//finish up rendering
							afterRenderings = Enumerable.Range(0, changedDmms.Count).Select(I => RenderOldMap(I)).ToList();
							try
							{
								await Task.WhenAll(afterRenderings).ConfigureAwait(false);
							}
							catch (Exception)
							{
								//see above
							}
							//done with the repo at this point
						}
						
						//collect results and errors
						async Task<MapDiff> GetResult(int i)
						{
							var beforeTask = beforeRenderings[i];
							var afterTask = afterRenderings[i];

							RenderResult GetRenderingResult(Task<RenderResult> task)
							{
								if (task.Exception != null)
								{
									errors.Add(task.Exception);
									return null;
								}
								return task.Result;
							};

							var r1 = GetRenderingResult(beforeTask);
							var r2 = GetRenderingResult(afterTask);
							var result = new MapDiff
							{
								RepositoryId = pullRequest.Base.Repository.Id,
								PullRequestNumber = pullRequest.Number,
								FileId = i,
								MapPath = r1?.InputPath ?? r2.InputPath,
								MapRegion = r1?.MapRegion ?? r2?.MapRegion
							};

							async Task<byte[]> ReadMapImage(string path)
							{
								if (path != null && await currentIOManager.FileExists(path, cancellationToken).ConfigureAwait(false))
									return await currentIOManager.ReadAllBytes(path, cancellationToken).ConfigureAwait(false);
								return null;
							}

							var readBeforeTask = ReadMapImage(r1?.InputPath);
							result.AfterImage = new Image { Data = await ReadMapImage(r2?.InputPath).ConfigureAwait(false) };
							result.BeforeImage = new Image { Data = await readBeforeTask.ConfigureAwait(false) };

							return result;
						}

						await generatingCommentTask.ConfigureAwait(false);

						var results = Enumerable.Range(0, changedDmms.Count).Select(x => GetResult(x)).ToList();
						await Task.WhenAll(results).ConfigureAwait(false);

						//delete old renderings
						var deleter = new MapDiff
						{
							PullRequestNumber = pullRequest.Number,
							RepositoryId = pullRequest.Base.Repository.Id
						};

						StringBuilder commentBuilder = null;
						int formatterCount = 0;

						Task saveTask;
						var databaseContext = serviceProvider.GetRequiredService<IDatabaseContext>();
						using (databaseContext.LockToCallStack(cancellationToken))
						{
							databaseContext.MapDiffSets.Remove(deleter);
							var finalDiffs = results.Select(x => x.Result).ToList();
							foreach (var I in finalDiffs)
							{
								if (commentBuilder == null)
									commentBuilder = new StringBuilder(String.Format(CultureInfo.InvariantCulture, "{0} | {1} | {2} | {3}{4}--- | --- | --- | ---", stringLocalizer["Map"], stringLocalizer["Old"], stringLocalizer["New"], stringLocalizer["Status"], Environment.NewLine));

								var beforeUrl = String.Concat(baseUrl, FilesController.RouteTo(pullRequest, formatterCount, "before"));
								var afterUrl = String.Concat(baseUrl, FilesController.RouteTo(pullRequest, formatterCount, "after"));
								var logsUrl = String.Concat(baseUrl, FilesController.RouteTo(pullRequest, formatterCount, "logs"));
								commentBuilder.Append(String.Format(CultureInfo.InvariantCulture, "{0}{1} | ![]({2}) | ![]({3}) | {7} | {6} | [{4}]({5})", Environment.NewLine, I.MapPath, beforeUrl, afterUrl, stringLocalizer["Logs"], logsUrl, I.BeforeImage != null ? (I.AfterImage != null ? stringLocalizer["Modified"] : stringLocalizer["Deleted"]) : stringLocalizer["Created"], I.MapRegion?.ToString() ?? stringLocalizer["ALL"]));
								databaseContext.MapDiffSets.Add(I);
							}
							
							saveTask = databaseContext.Save(cancellationToken);
						}
						var comment = String.Format(CultureInfo.CurrentCulture, "{0}<br>{1}<br>{2}", commentBuilder, stringLocalizer["Last updated from merging commit {2} into {3}", pullRequest.Head.Sha, pullRequest.Base.Sha], stringLocalizer["Full job logs avaiabled [here]({0})", String.Concat(baseUrl, FilesController.RouteTo(pullRequest))]);
						await gitHubManager.CreateSingletonComment(pullRequest, comment, cancellationToken).ConfigureAwait(false);
						await saveTask.ConfigureAwait(false);
					}
					catch (Exception e)
					{
						//if this is the only exception, throw it directly, otherwise pile it in the exception collection
						if (errors.Count == 0)
							throw;
						errors.Add(e);
						cts.Cancel();
					}
					finally
					{
						lock (mapDiffOperations)
							if (mapDiffOperations.TryGetValue(requestIdentifier, out CancellationTokenSource maybeOurOperation) && maybeOurOperation == cts)
								mapDiffOperations.Remove(requestIdentifier);
						//throw all generator errors at once, because we can allow things to continue if some fail
						if (errors.Count > 0)
						{
							var e = new AggregateException(String.Format(CultureInfo.CurrentCulture, "Repo: {0}/{1}, PR: {2} (#{3}) Base: {4} ({5}), HEAD: {6}", pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Name, pullRequest.Title, pullRequest.Number, pullRequest.Base.Sha, pullRequest.Base.Label, pullRequest.Head.Sha), errors);
							logger.LogError(e, "Generation errors occurred!");
						}
					}
				}
			}
		}

		public void ProcessPayload(PullRequestEventPayload payload, IUrlHelper urlHelper)
		{
			if (payload.Action == "opened" || payload.Action == "synchronize")
			{
				var basePath = urlHelper.ActionContext.HttpContext.Request.Host + urlHelper.ActionContext.HttpContext.Request.PathBase;
				backgroundJobClient.Enqueue(() => GenerateMapDiff(payload.PullRequest.Number, payload.Repository.Id, basePath, JobCancellationToken.Null));
			}
		}
		public async Task ProcessPayload(IssueCommentPayload payload, IGitHubManager gitHubManager, IUrlHelper urlHelper)
		{
			if (payload.Action != "created" || payload.Comment.Body != null)
				return;
			var user = await gitHubManager.GetUser().ConfigureAwait(false);
			if (payload.Comment.Body.Contains(String.Format(CultureInfo.InvariantCulture, "@{0}", user.Login)))
			{
				var basePath = urlHelper.ActionContext.HttpContext.Request.Host + urlHelper.ActionContext.HttpContext.Request.PathBase;
				backgroundJobClient.Enqueue(() => GenerateMapDiff(payload.Issue.Number, payload.Repository.Id, basePath, JobCancellationToken.Null));
			}
		}
	}
}
