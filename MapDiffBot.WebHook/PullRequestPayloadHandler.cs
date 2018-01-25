using MapDiffBot.Generator;
using Microsoft.AspNet.WebHooks;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see cref="IPayloadHandler"/> for pull_request events
	/// </summary>
	sealed class PullRequestPayloadHandler : IPayloadHandler
	{
		/// <summary>
		/// The config key used for GitHub access tokens
		/// </summary>
		const string AccessTokenConfigKey = "AccessToken";
		/// <summary>
		/// The config key used for imgur client ids
		/// </summary>
		const string ImgurIDConfigKey = "ImgurID";
		/// <summary>
		/// The config key used for imgur client secrets
		/// </summary>
		const string ImgurSecretConfigKey = "ImgurSecret";

		/// <summary>
		/// The <see cref="IFileUploader"/> for the <see cref="PullRequestPayloadHandler"/>
		/// </summary>
		static readonly IFileUploader fileUploader = new LocalFileUploader();
		/// <summary>
		/// The <see cref="IGeneratorFactory"/> for the <see cref="PullRequestPayloadHandler"/>
		/// </summary>
		static readonly IGeneratorFactory generatorFactory = new GeneratorFactory();
		/// <summary>
		/// The <see cref="IGitHub"/> for the <see cref="PullRequestPayloadHandler"/>
		/// </summary>
		static readonly IGitHub gitHub = new GitHub();

		/// <summary>
		/// The <see cref="IRepositoryManager"/> for the <see cref="PullRequestPayloadHandler"/>
		/// </summary>
		readonly IRepositoryManager repositoryManager;

		/// <inheritdoc />
		public string EventType => "pull_request";

		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="PullRequestPayloadHandler"/>
		/// </summary>
		readonly IIOManager ioManager;

		/// <summary>
		/// <see cref="Dictionary{TKey, TValue}"/> of operation name to their <see cref="CancellationToken"/>
		/// </summary>
		readonly Dictionary<string, CancellationTokenSource> mapDiffOperations;

		/// <summary>
		/// Construct a <see cref="PullRequestPayloadHandler"/>
		/// </summary>
		/// <param name="_ioManager">The value of <see cref="ioManager"/></param>
		/// <param name="_logger">Unused</param>
		[SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "_logger")]
		public PullRequestPayloadHandler(IIOManager _ioManager, ILogger _logger)
		{
			ioManager = new ResolvingIOManager(_ioManager ?? throw new ArgumentNullException(nameof(_ioManager)), "MapDiffs");

			lock (generatorFactory)
				if (repositoryManager == null)
					repositoryManager = new RepositoryManager(ioManager);

			mapDiffOperations = new Dictionary<string, CancellationTokenSource>();
		}

		/// <summary>
		/// Uploads <see cref="IMapDiff"/> images to imgur and generates a markdown table for a diff comparison
		/// </summary>
		/// <param name="diffs">The <see cref="IMapDiff"/>s in the table</param>
		/// <param name="config">The <see cref="IWebHookReceiverConfig"/> for the operation</param>
		/// <param name="pullRequest">The <see cref="PullRequest"/> being commented on</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the markdown table <see cref="string"/></returns>
		static async Task<string> UploadDiffsAndGenerateMarkdown(IEnumerable<IMapDiff> diffs, IWebHookReceiverConfig config, PullRequest pullRequest, CancellationToken token)
		{
			StringBuilder result = null;
			List<Task<string>> tasks = null;
			string imgurSecret = null, imgurID = null;
			int formatterCount = 0;
			foreach (var I in diffs)
			{
				if (result == null)
				{
					imgurID = await config.GetReceiverConfigAsync(GitHubWebHookReceiver.ReceiverName, ImgurIDConfigKey);
					imgurSecret = await config.GetReceiverConfigAsync(GitHubWebHookReceiver.ReceiverName, ImgurSecretConfigKey);
					result = new StringBuilder(String.Format(CultureInfo.CurrentCulture, "Maps with diff:{0}", Environment.NewLine));
					tasks = new List<Task<string>>();
				}

				if(I.BeforePath == null && I.AfterPath == null)
				{
					result.Append(String.Format(CultureInfo.InvariantCulture, "{0}<details><summary>{1}</summary>{0}{0}Old | New | Status{0}--- | --- | ---{0}Unavailable | Unavailable | {2}{0}{0}</details>", Environment.NewLine, I.OriginalMapName, "Errored"));
					continue;
				}

				result.Append(String.Format(CultureInfo.InvariantCulture, "{0}<details><summary>{1}</summary>{0}{0}Old | New | Status{0}--- | --- | ---{0}![]({{{2}}}) | ![]({{{3}}}) | {4}{0}{0}</details>", Environment.NewLine, I.OriginalMapName, formatterCount++, formatterCount++, I.BeforePath != null ? (I.AfterPath != null ? "Modified" : "Deleted") : "Created"));

				if (I.BeforePath != null)
					tasks.Add(fileUploader.Upload(I.BeforePath, String.Format(CultureInfo.InvariantCulture, "{0}/{1}", imgurID, imgurSecret), token));
				else
					tasks.Add(Task.FromResult<string>(null));

				if (I.AfterPath != null)
					tasks.Add(fileUploader.Upload(I.AfterPath, String.Format(CultureInfo.InvariantCulture, "{0}/{1}", imgurID, imgurSecret), token));
				else
					tasks.Add(Task.FromResult<string>(null));
			}

			await Task.WhenAll(tasks);

			var comment = String.Format(CultureInfo.InvariantCulture, result.ToString(), tasks.Select(x => x.Result).ToArray());
			comment = String.Format(CultureInfo.CurrentCulture, "{0}{1}<br>Last updated from merging commit {2} into {3}", comment, Environment.NewLine, pullRequest.Head.Sha, pullRequest.Base.Sha);
			return comment;
		}

		/// <summary>
		/// Generates a map diff comment for the specified <paramref name="payload"/>
		/// </summary>
		/// <param name="payload">The <see cref="PullRequestEventPayload"/> to possibly generate a diff for</param>
		/// <param name="config">The <see cref="IWebHookReceiverConfig"/> for the operation</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		async Task GenerateMapDiff(PullRequestEventPayload payload, IWebHookReceiverConfig config, CancellationToken token)
		{			
			var requestIdentifier = ioManager.ConcatPath(payload.Repository.Owner.Login, payload.Repository.Name, payload.PullRequest.Number.ToString());
			var currentIOManager = new ResolvingIOManager(ioManager, ioManager.ConcatPath("Operations", requestIdentifier));
			//Generate our own cancellation token for rolling builds of the same PR
			using (var cts = new CancellationTokenSource())
			using (token.Register(() => cts.Cancel()))
			{
				token = cts.Token;

				//cancel what was running before for this PR and restart
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

				try
				{
					//load the github API key
					gitHub.AccessToken = await config.GetReceiverConfigAsync(GitHubWebHookReceiver.ReceiverName, AccessTokenConfigKey);

					//check if the PR is mergeable, if not, don't render it
					bool? mergeable = payload.PullRequest.Mergeable;
					for (var I = 0; mergeable == null && I < 5; ++I)
					{
						await Task.Delay(5000);
						mergeable = await gitHub.CheckPullRequestMergeable(payload.Repository, payload.PullRequest.Number);
						token.ThrowIfCancellationRequested();
					}
					if (mergeable == null || !mergeable.Value)
						return;
					
					const string ErrorLogFile = "error_log.txt";
					var outputDirectory = currentIOManager.ResolvePath(".");
					var results = new List<IMapDiff>();
					var errors = new List<Exception>();
					try
					{
						List<Task<string>> beforeRenderings, afterRenderings;

						//get the list of files changed by the PR
						var changedMaps = await gitHub.GetChangedMapFiles(payload.Repository, payload.PullRequest.Number);
						if (changedMaps.Count == 0)
							return;

						//lock the repository the PR belongs to
						using (var repo = await repositoryManager.GetRepository(payload.Repository.Owner.Login, payload.Repository.Name, token))
						{
							//prep the outputDirectory
							async Task DirectoryPrep()
							{
								await currentIOManager.DeleteDirectory(".", token);
								await currentIOManager.CreateDirectory(".", token);
							};

							//fetch base commit if necessary and check it out, fetch pull request
							var baseSha = payload.PullRequest.Base.Sha;
							var dirPrepTask = DirectoryPrep();

							async Task<string> FindDME()
							{
								var dmes = await currentIOManager.GetFilesWithExtension(repo.Path, "dme", token);
								if (dmes.Count < 2)
									return null;
								var lowerRepo = payload.Repository.Name.ToLower(CultureInfo.InvariantCulture);
								foreach (var I in dmes)
									if (I.ToLower(CultureInfo.InvariantCulture).Contains(lowerRepo))
										return I;
								//meh
								return dmes.First();
							};

							var dmeToUseTask = FindDME();

							if (!await repo.ContainsCommit(baseSha, token))
								await repo.Fetch(token);
							await repo.FetchPullRequest(payload.PullRequest.Number, token);

							var dmeToUse = await dmeToUseTask;

							var checkoutTask = repo.Checkout(baseSha, token);

							await checkoutTask;
							await dirPrepTask;
							var oldMapPaths = new List<string>()
							{
								Capacity = changedMaps.Count
							};

							//first copy all modified/deleted maps to the same location with the .old_map_diff_bot extension
							foreach (var path in changedMaps)
							{
								var originalPath = currentIOManager.ConcatPath(repo.Path, path);
								if (await currentIOManager.FileExists(originalPath, token))
								{
									var oldMapPath = String.Format(CultureInfo.InvariantCulture, "{0}.old_map_diff_bot", originalPath);
									await currentIOManager.CopyFile(originalPath, oldMapPath, token);
									oldMapPaths.Add(oldMapPath);
								}
								else
									oldMapPaths.Add(null);
							}

							//generate the merge commit ourselves since we can't get it from GitHub
							await repo.Merge(payload.PullRequest.Head.Sha, token);

							afterRenderings = new List<Task<string>>()
							{
								Capacity = changedMaps.Count
							};
							var mapRegions = Enumerable.Repeat<MapRegion>(null, changedMaps.Count).ToList();
							var mapDiffer = generatorFactory.CreateGenerator(dmeToUse);

							//Generate MapRegions for modified maps and render all new maps
							async Task<string> DiffAndRenderNewMap(int I)
							{
								var originalPath = currentIOManager.ConcatPath(repo.Path, changedMaps[I]);
								if (!await currentIOManager.FileExists(originalPath, token))
									return null;
								if (oldMapPaths[I] != null)
									mapRegions[I] = await mapDiffer.GetDifferences(oldMapPaths[I], originalPath, repo.Path, token);
								return await mapDiffer.RenderMap(originalPath, mapRegions[I], repo.Path, outputDirectory, "after", token);
							};
							for (var I = 0; I < changedMaps.Count; ++I)
								afterRenderings.Add(DiffAndRenderNewMap(I));

							//finish up before we go back to the base branch
							try
							{
								await Task.WhenAll(afterRenderings);
							}
							catch (Exception)
							{
								//at this point everything is done but some have failed
								//we'll handle it later
							}
							await repo.Checkout(baseSha, token);

							beforeRenderings = new List<Task<string>>()
							{
								Capacity = changedMaps.Count
							};

							//render all old maps using the MapRegions we got if they exist
							for (var I = 0; I < changedMaps.Count; ++I)
							{
								Task<string> oldTask;
								var oldPath = oldMapPaths[I];
								if (oldMapPaths != null)
									oldTask = mapDiffer.RenderMap(oldPath, mapRegions[I], repo.Path, outputDirectory, "before", token);
								else
									oldTask = Task.FromResult<string>(null);
								beforeRenderings.Add(oldTask);
							}

							//finish up rendering
							try
							{
								await Task.WhenAll(beforeRenderings);
							}
							catch (Exception)
							{
								//see above
							}
							//done with the repo at this point
						}
						
						//collect results and errors
						for (var I = 0; I < changedMaps.Count; ++I)
						{
							var beforeTask = beforeRenderings[I];
							var afterTask = afterRenderings[I];

							string GetRenderingResult(Task<string> task)
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
							if (r1 != null || r2 != null)
								results.Add(new MapDiff(currentIOManager.GetFileNameWithoutExtension(changedMaps[I]), r1, r2));
						}

						//nothing to do if nothing
						if (results.Count == 0)
							return;

						//and the finishers
						var comment = await UploadDiffsAndGenerateMarkdown(results, config, payload.PullRequest, token);
						await gitHub.CreateSingletonComment(payload.Repository, payload.Number, comment);
					}
					catch (Exception e)
					{
						//if this is the only exception, throw it directly, otherwise pile it in the exception collection
						if (errors.Count == 0)
						{
							e.Data[Logger.OutputFileExceptionKey] = currentIOManager.ConcatPath(outputDirectory, ErrorLogFile);
							throw;
						}
						errors.Add(e);
						cts.Cancel();
					}
					finally
					{
						//throw all generator errors at once, because we can allow things to continue if some fail
						if (errors.Count > 0)
						{
							var e = new AggregateException(String.Format(CultureInfo.CurrentCulture, "Generation errors occurred! Repo: {0}/{1}, PR: {2} (#{3}) Base: {4} ({5}), HEAD: {6}", payload.Repository.Owner.Login, payload.Repository.Name, payload.PullRequest.Title, payload.PullRequest.Number, payload.PullRequest.Base.Sha, payload.PullRequest.Base.Label, payload.PullRequest.Head.Sha), errors);
							e.Data[Logger.OutputFileExceptionKey] = currentIOManager.ConcatPath(outputDirectory, ErrorLogFile); ;
							throw e;
						}
					}
				}
				finally
				{
					//allow the next thing in queue to proceed
					lock (mapDiffOperations)
						if (mapDiffOperations.TryGetValue(requestIdentifier, out CancellationTokenSource maybeOurOperation) && maybeOurOperation == cts)
							mapDiffOperations.Remove(requestIdentifier);
				}
			}
		}

		/// <inheritdoc />
		public async Task Run(JObject payload, IWebHookReceiverConfig config, CancellationToken token)
		{
			if (payload == null)
				throw new ArgumentNullException(nameof(payload));
			if (config == null)
				throw new ArgumentNullException(nameof(config));

			var truePayload = new SimpleJsonSerializer().Deserialize<PullRequestEventPayload>(payload.ToString());

			switch (truePayload.Action)
			{
				case "opened":
				case "synchronize":
					await GenerateMapDiff(truePayload, config, token);
					break;
				default:
					throw new NotImplementedException();
			}
		}
	}
}
