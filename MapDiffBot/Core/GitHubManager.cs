using MapDiffBot.Configuration;
using MapDiffBot.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
	sealed class GitHubManager : IGitHubManager
	{
		/// <summary>
		/// Cookie used to store <see cref="AccessTokenEntry"/>s
		/// </summary>
		const string AuthorizationCookie = "1df00c9b-be1a-4274-a8ab-db0e575ff589";

		/// <summary>
		/// The <see cref="GitHubConfiguration"/> for the <see cref="GitHubManager"/>
		/// </summary>
		GitHubConfiguration gitHubConfiguration;
		/// <summary>
		/// The <see cref="GitHubConfiguration"/> for the <see cref="GitHubManager"/>
		/// </summary>
		readonly IGitHubClientFactory gitHubClientFactory;
		/// <summary>
		/// The <see cref="IDatabaseContext"/> for the <see cref="GitHubManager"/>
		/// </summary>
		readonly IDatabaseContext databaseContext;
		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="GitHubManager"/>
		/// </summary>
		readonly ILogger<GitHubManager> logger;

		public GitHubManager(IOptions<GitHubConfiguration> gitHubConfigurationOptions, IGitHubClientFactory gitHubClientFactory, IDatabaseContext databaseContext, ILogger<GitHubManager> logger)
		{
			gitHubConfiguration = gitHubConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(gitHubConfigurationOptions));
			this.gitHubClientFactory = gitHubClientFactory ?? throw new ArgumentNullException(nameof(gitHubClientFactory));
			this.databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Create a <see cref="IGitHubClient"/> based on a <see cref="Repository.Id"/> in a <see cref="Installation"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="Repository.Id"/> of a <see cref="Repository"/> in the <see cref="Installation"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in a new <see cref="IGitHubClient"/></returns>
		async Task<IGitHubClient> CreateInstallationClient(long repositoryId, CancellationToken cancellationToken)
		{
			IReadOnlyList<Octokit.Installation> gitHubInstalls;
			List<Models.Installation> allKnownInstalls;
			IGitHubClient client;
			var installation = await databaseContext.Installations.Where(x => x.Repositories.Any(y => y.Id == repositoryId)).ToAsyncEnumerable().FirstOrDefault(cancellationToken).ConfigureAwait(false);

			if (installation != default(Models.Installation))
			{
				if (installation.AccessTokenExpiry < DateTimeOffset.UtcNow)
				{
					var newToken = await gitHubClientFactory.CreateAppClient().GitHubApps.CreateInstallationToken(installation.InstallationId).ConfigureAwait(false);
					installation.AccessToken = newToken.Token;
					installation.AccessTokenExpiry = newToken.ExpiresAt;
					await databaseContext.Save(cancellationToken).ConfigureAwait(false);
				}
				return gitHubClientFactory.CreateOauthClient(installation.AccessToken);
			}

			//do a discovery
			client = gitHubClientFactory.CreateAppClient();

			//remove bad installs while we're here
			var allKnownInstallsTask = databaseContext.Installations.ToAsyncEnumerable().ToList();
			gitHubInstalls = await client.GitHubApps.GetAllInstallationsForCurrent().ConfigureAwait(false);
			allKnownInstalls = await allKnownInstallsTask.ConfigureAwait(false);
			databaseContext.Installations.RemoveRange(allKnownInstalls.Where(x => !gitHubInstalls.Any(y => y.Id == x.InstallationId)));

			//add new installs for those that aren't
			var installsToAdd = gitHubInstalls.Where(x => !allKnownInstalls.Any(y => y.InstallationId == x.Id));

			async Task<Models.Installation> CreateAccessToken(Octokit.Installation newInstallation)
			{
				//TODO: Implement this in octokit
				//If you're here and wondering why we're not using pagination, it's because YOU HAVEN'T PORTED THIS TO OCTOKIT YET

				var installationToken = await client.GitHubApps.CreateInstallationToken(newInstallation.Id).ConfigureAwait(false);
				var entity = new Models.Installation
				{
					InstallationId = newInstallation.Id,
					AccessToken = installationToken.Token,
					AccessTokenExpiry = installationToken.ExpiresAt,
					Repositories = new List<InstallationRepository>()
				};

				var repos = await gitHubClientFactory.GetInstallationRepositories(installationToken.Token, cancellationToken).ConfigureAwait(false);

				entity.Repositories.AddRange(repos.Select(x => new InstallationRepository { Id = x.Id }));

				return entity;
			}

			var newEntities = await Task.WhenAll(installsToAdd.Select(x => CreateAccessToken(x))).ConfigureAwait(false);

			await databaseContext.Installations.AddRangeAsync(newEntities).ConfigureAwait(false);
			await databaseContext.Save(cancellationToken).ConfigureAwait(false);
			//its either in newEntities now or it doesn't exist
			return gitHubClientFactory.CreateOauthClient(newEntities.First(x => x.Repositories.Any(y => y.Id == repositoryId)).AccessToken);
		}

		/// <inheritdoc />
		public async Task<bool> CheckAuthorization(string repoOwner, string repoName, IRequestCookieCollection cookies, CancellationToken cancellationToken)
		{
			if (!cookies.TryGetValue(AuthorizationCookie, out string cookieGuid))
				return false;

			if (!Guid.TryParse(AuthorizationCookie, out Guid guid))
				return false;

			logger.LogTrace("Check authorization");

			//cleanup
			var now = DateTimeOffset.Now;
			var everything = await databaseContext.UserAccessTokens.ToAsyncEnumerable().ToList().ConfigureAwait(false);
			var toRemove = everything.Where(x => x.Expiry < now);
			databaseContext.UserAccessTokens.RemoveRange(toRemove);
			await databaseContext.Save(cancellationToken).ConfigureAwait(false);

			var entry = everything.Where(x => x.Id == guid && x.Expiry >= now).FirstOrDefault();
			if (entry == default(UserAccessToken))
				return false;

			try
			{
				await gitHubClientFactory.CreateOauthClient(entry.AccessToken).User.Current().ConfigureAwait(false);
			}
			catch (ForbiddenException)
			{
				return false;
			}

			return true;
		}

		/// <inheritdoc />
		public Task<string> CompleteAuthorization(HttpRequest request, IResponseCookies cookies, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestChangedFiles(PullRequest pullRequest, CancellationToken cancellationToken)
		{
			if (pullRequest == null)
				throw new ArgumentNullException(nameof(pullRequest));
			logger.LogTrace("Get changed files for {0}/{1} #{2}", pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Name, pullRequest.Number);
			var gitHubClient = await CreateInstallationClient(pullRequest.Base.Repository.Id, cancellationToken).ConfigureAwait(false);
			return await gitHubClient.PullRequest.Files(pullRequest.Base.Repository.Id, pullRequest.Number).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public async Task CreateSingletonComment(PullRequest pullRequest, string body, CancellationToken cancellationToken)
		{
			if (pullRequest == null)
				throw new ArgumentNullException(nameof(pullRequest));
			if (body == null)
				throw new ArgumentNullException(nameof(body));
			if (String.IsNullOrWhiteSpace(body))
				throw new ArgumentOutOfRangeException(nameof(body), body, "Body must not be empty!");


			var client = await CreateInstallationClient(pullRequest.Base.Repository.Id, cancellationToken).ConfigureAwait(false);

			var openCommentsTask = client.Issue.Comment.GetAllForIssue(pullRequest.Base.Repository.Id, pullRequest.Number);

			cancellationToken.ThrowIfCancellationRequested();

			var ourUser = await GetUser().ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

			var openComments = await openCommentsTask.ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

			foreach (var I in openComments)
				if (I.User.Id == ourUser.Id)
				{
					logger.LogTrace("Update comment on {1}/{2} #{3}. Old: {4}. New: {0}", body, pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Owner.Name, pullRequest.Number, I.Body);
					await client.Issue.Comment.Update(pullRequest.Base.Repository.Id, I.Id, body).ConfigureAwait(false);
					return;
				}

			logger.LogTrace("Create comment on {1}/{2} #{3}: {0}", body, pullRequest.Base.Repository.Owner.Login, pullRequest.Base.Repository.Owner.Name, pullRequest.Number);
			await client.Issue.Comment.Create(pullRequest.Base.Repository.Id, pullRequest.Number, body).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public void ExpireAuthorization(IResponseCookies cookies)
		{
			logger.LogTrace("Expire auth cookies");
			cookies.Delete(AuthorizationCookie);
		}

		/// <inheritdoc />
		public Uri GetAuthorizationURL(Uri callbackURL, string state)
		{
			if (callbackURL == null)
				throw new ArgumentNullException(nameof(callbackURL));
			logger.LogTrace("Create auth url to {0}. State: {1}", callbackURL, state);
			return gitHubClientFactory.CreateAppClient().Oauth.GetGitHubLoginUrl(new OauthLoginRequest(gitHubConfiguration.OauthClientID) { State = state, RedirectUri = callbackURL });
		}

		/// <inheritdoc />
		public async Task<PullRequest> GetPullRequest(long repositoryId, int pullRequestNumber, CancellationToken cancellationToken)
		{
			logger.LogTrace("Get pull request #{0} on repository {1}", pullRequestNumber, repositoryId);
			var client = await CreateInstallationClient(repositoryId, cancellationToken).ConfigureAwait(false);
			return await client.PullRequest.Get(repositoryId, pullRequestNumber).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public Task<User> GetUser()
		{
			logger.LogTrace("Get current user");
			return gitHubClientFactory.CreateAppClient().User.Current();
		}
	}
}
