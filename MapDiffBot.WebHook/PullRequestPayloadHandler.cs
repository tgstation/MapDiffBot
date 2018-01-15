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
		/// The config key used for imgur client secrets
		/// </summary>
		const string ImgurSecretConfigKey = "ImgurKey";

		/// <summary>
		/// The <see cref="IFileUploader"/> for the <see cref="PullRequestPayloadHandler"/>
		/// </summary>
		static readonly IFileUploader fileUploader = new ImgurFileUploader();
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
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the markdown table <see cref="string"/></returns>
		async Task<string> UploadDiffsAndGenerateMarkdown(IEnumerable<IMapDiff> diffs, IWebHookReceiverConfig config, CancellationToken token)
		{
			StringBuilder result = null;
			List<Task<string>> tasks = null;
			string imgurSecret = null;
			int formatterCount = 0;
			foreach (var I in diffs)
			{
				if (result == null)
				{
					imgurSecret = await config.GetReceiverConfigAsync(GitHubWebHookReceiver.ReceiverName, ImgurSecretConfigKey);
					result = new StringBuilder(String.Format(CultureInfo.InvariantCulture, "Map | Old | New | Status{0}--- | --- | --- | ---", Environment.NewLine));
					tasks = new List<Task<string>>();
				}

				result.Append(String.Format(CultureInfo.InvariantCulture, "{0}{1} | ![]({{{2}}}) | ![]({{{3}}}) | {4}", Environment.NewLine, I.BeforePath != null ? ioManager.GetFileName(I.BeforePath) : ioManager.GetFileName(I.AfterPath), ++formatterCount, ++formatterCount, I.BeforePath != null ? (I.AfterPath != null ? "Modified" : "Deleted") : "Created"));

				tasks.Add(fileUploader.Upload(I.AfterPath, imgurSecret, token));
			}

			await Task.WhenAll(tasks);

			return String.Format(CultureInfo.InvariantCulture, result.ToString(), tasks.Select(x => x.Result).ToArray());
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
			var requestIdentifier = String.Concat(payload.Repository.Owner.Login, payload.Repository.Name, payload.PullRequest.Number);
			var currentIOManager = new ResolvingIOManager(ioManager, requestIdentifier);
			//Generate our own cancellation token for rolling builds of the same PR
			using (var cts = new CancellationTokenSource())
			{
				token.Register(() => cts.Cancel());
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

				var results = new List<IMapDiff>();
				using (var repo = await repositoryManager.GetRepository(payload.Repository.Owner.Login, payload.Repository.Name, token))
				{
					var baseSha = payload.PullRequest.Base.Sha;
					if (!await repo.ContainsCommit(baseSha, token))
						await repo.Fetch(token);
					
					await repo.FetchPullRequest(payload.PullRequest.Number, token);

					var mapDiffer = generatorFactory.CreateGenerator();

					foreach (var path in await gitHub.GetChangedMapFiles(payload.Repository, payload.PullRequest.Number, token))
					{
						await repo.Checkout(baseSha, token);

						var originalPath = currentIOManager.ConcatPath(repo.Path, path);
						string oldMapPath;
						if (await currentIOManager.FileExists(originalPath, token))
						{
							oldMapPath = currentIOManager.ConcatPath(originalPath, ".old_map_diff_bot");
							await currentIOManager.CopyFile(originalPath, oldMapPath, token);
						}
						else
							oldMapPath = null;

						await repo.Checkout(payload.PullRequest.MergeCommitSha, token);
						
						try
						{
							results.Add(await mapDiffer.GenerateDiff(oldMapPath, originalPath, token));
						}
						catch(OperationCanceledException)
						{
							throw;
						}
						catch
						{
							//TODO specific exception
						}
						if (oldMapPath != null)
							await currentIOManager.DeleteFile(oldMapPath, token);
					}
				}
				
				var comment = await UploadDiffsAndGenerateMarkdown(results, config, token);

				gitHub.AccessToken = await config.GetReceiverConfigAsync(GitHubWebHookReceiver.ReceiverName, AccessTokenConfigKey);
				await gitHub.CreateSingletonComment(payload.Repository, payload.Number, comment, token);
			}
		}

		/// <inheritdoc />
		public async Task Run(JObject payload, IWebHookReceiverConfig config, CancellationToken token)
		{
			if (payload == null)
				throw new ArgumentNullException(nameof(payload));

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
