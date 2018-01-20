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
					gitHub.AccessToken = await config.GetReceiverConfigAsync(GitHubWebHookReceiver.ReceiverName, AccessTokenConfigKey);

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
						using (var repo = await repositoryManager.GetRepository(payload.Repository.Owner.Login, payload.Repository.Name, token))
						{
							var baseSha = payload.PullRequest.Base.Sha;
							if (!await repo.ContainsCommit(baseSha, token))
								await repo.Fetch(token);

							await repo.FetchPullRequest(payload.PullRequest.Number, token);

							Task cdt = null;

							var mapDiffer = generatorFactory.CreateGenerator();

							foreach (var path in await gitHub.GetChangedMapFiles(payload.Repository, payload.PullRequest.Number))
							{
								await repo.Checkout(baseSha, token);

								async Task handleCreateDirectory()
								{
									await currentIOManager.DeleteDirectory(".", token);
									await currentIOManager.CreateDirectory(".", token);
								};

								if (cdt == null)
									cdt = handleCreateDirectory();

								var originalPath = currentIOManager.ConcatPath(repo.Path, path);
								string oldMapPath;
								if (await currentIOManager.FileExists(originalPath, token))
								{
									oldMapPath = String.Format(CultureInfo.InvariantCulture, "{0}.old_map_diff_bot", originalPath);
									await cdt;
									await currentIOManager.CopyFile(originalPath, oldMapPath, token);
								}
								else
									oldMapPath = null;

								await repo.Merge(payload.PullRequest.Head.Sha, token);

								try
								{
									results.Add(await mapDiffer.GenerateDiff(oldMapPath, originalPath, repo.Path, outputDirectory, token));
								}
								catch (GeneratorException e)
								{
									results.Add(new MapDiff(currentIOManager.GetFileNameWithoutExtension(originalPath ?? oldMapPath), null, null));
									errors.Add(e);
								}
							}
						}

						if (results.Count == 0 || errors.Count == results.Count)
							return;

						var comment = await UploadDiffsAndGenerateMarkdown(results, config, payload.PullRequest, token);
						await gitHub.CreateSingletonComment(payload.Repository, payload.Number, comment);
					}
					catch (Exception e)
					{
						if (errors.Count == 0)
						{
							e.Data[Logger.OutputFileExceptionKey] = currentIOManager.ConcatPath(outputDirectory, ErrorLogFile);
							throw;
						}
						errors.Add(e);
					}
					finally
					{
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
