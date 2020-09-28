using MapDiffBot.Configuration;
using MapDiffBot.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
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
	sealed class GitHubManager : IGitHubManager
#pragma warning restore CA1812
	{
		/// <summary>
		/// Cookie used to store <see cref="UserAccessToken"/>s
		/// </summary>
		const string AuthorizationCookie = "1df00c9b-be1a-4274-a8ab-db0e575ff589";
		/// <summary>
		/// How long the <see cref="AuthorizationCookie"/> will last for
		/// </summary>
		const int AccessTokenExpiryDays = 3;

		/// <summary>
		/// The <see cref="GitHubConfiguration"/> for the <see cref="GitHubManager"/>
		/// </summary>
		readonly GitHubConfiguration gitHubConfiguration;
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

		/// <summary>
		/// Construct a <see cref="GitHubManager"/>
		/// </summary>
		/// <param name="gitHubConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing the value of <see cref="gitHubConfiguration"/></param>
		/// <param name="gitHubClientFactory">The value of <see cref="gitHubClientFactory"/></param>
		/// <param name="databaseContext">The value of <see cref="databaseContext"/></param>
		/// <param name="logger">The value of <see cref="logger"/></param>
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
			var installation = await databaseContext.Installations.Where(x => x.Repositories.Any(y => y.Id == repositoryId)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

			if (installation != null)
			{
				if (installation.AccessTokenExpiry < DateTimeOffset.Now.AddMinutes(-1))
				{
					var newToken = await gitHubClientFactory.CreateAppClient().GitHubApps.CreateInstallationToken(installation.Id).ConfigureAwait(false);
					installation.AccessToken = newToken.Token;
					installation.AccessTokenExpiry = newToken.ExpiresAt;
					await databaseContext.Save(cancellationToken).ConfigureAwait(false);
					return gitHubClientFactory.CreateOauthClient(newToken.Token);
				}
				return gitHubClientFactory.CreateOauthClient(installation.AccessToken);
			}

			//do a discovery
			client = gitHubClientFactory.CreateAppClient();

			//remove bad installs while we're here
			var allKnownInstallsTask = databaseContext.Installations.ToListAsync(cancellationToken);
			gitHubInstalls = await client.GitHubApps.GetAllInstallationsForCurrent().ConfigureAwait(false);
			allKnownInstalls = await allKnownInstallsTask.ConfigureAwait(false);
			databaseContext.Installations.RemoveRange(allKnownInstalls.Where(x => !gitHubInstalls.Any(y => y.Id == x.Id)));

			//add new installs for those that aren't
			var installsToAdd = gitHubInstalls.Where(x => !allKnownInstalls.Any(y => y.Id == x.Id));

			async Task<Models.Installation> CreateAccessToken(Octokit.Installation newInstallation)
			{
				var installationToken = await client.GitHubApps.CreateInstallationToken(newInstallation.Id).ConfigureAwait(false);
				var entity = new Models.Installation
				{
					Id = newInstallation.Id,
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
		public Task LoadInstallation(long repositoryId, CancellationToken cancellationToken) => CreateInstallationClient(repositoryId, cancellationToken);

		/// <inheritdoc />
		public async Task<AuthenticationLevel> CheckAuthorization(long repositoryId, IRequestCookieCollection cookies, CancellationToken cancellationToken)
		{
			if (!cookies.TryGetValue(AuthorizationCookie, out string cookieGuid))
				return AuthenticationLevel.LoggedOut;

			if (!Guid.TryParse(cookieGuid, out Guid guid))
				return AuthenticationLevel.LoggedOut;

			logger.LogTrace("Check authorization");

			//cleanup
			var now = DateTimeOffset.Now;
			var everything = await databaseContext.UserAccessTokens.Where(x => x.Expiry < DateTimeOffset.UtcNow).DeleteAsync(cancellationToken).ConfigureAwait(false);
			var entry = await databaseContext.UserAccessTokens.Where(x => x.Id == guid).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
			if (entry == default(UserAccessToken))
				return AuthenticationLevel.LoggedOut;
			
			try
			{
				var repoClientTask = CreateInstallationClient(repositoryId, cancellationToken);
				var userClient = gitHubClientFactory.CreateOauthClient(entry.AccessToken);
				var user = await userClient.User.Current().ConfigureAwait(false);
				var repoClient = await repoClientTask.ConfigureAwait(false);
				return await repoClient.Repository.Collaborator.IsCollaborator(repositoryId, user.Login).ConfigureAwait(false) ? AuthenticationLevel.Maintainer : AuthenticationLevel.User;
			}
			catch
			{
				return AuthenticationLevel.LoggedOut;
			}
		}

		/// <inheritdoc />
		public async Task<string> CompleteAuthorization(HttpRequest request, IResponseCookies cookies, CancellationToken cancellationToken)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));
			if (cookies == null)
				throw new ArgumentNullException(nameof(cookies));


			string code;
			if (!request.Query.TryGetValue("code", out StringValues stringValues) || stringValues.Count == 0)
				return null;
			code = stringValues.First();

			logger.LogTrace("CompleteAuthorization for with code: {0}", code);


			var otr = new OauthTokenRequest(gitHubConfiguration.OauthClientID, gitHubConfiguration.OauthSecret, code);
			var gitHubClient = gitHubClientFactory.CreateAppClient();
			var result = await gitHubClient.Oauth.CreateAccessToken(otr).ConfigureAwait(false);
			if (result.AccessToken == null)
				//user is fucking with us, don't even bother
				return null;

			var expiry = DateTimeOffset.UtcNow.AddDays(AccessTokenExpiryDays);
			var newEntry = new UserAccessToken
			{
				Id = Guid.NewGuid(),
				AccessToken = result.AccessToken,
				Expiry = expiry
			};

			await databaseContext.UserAccessTokens.AddAsync(newEntry, cancellationToken).ConfigureAwait(false);
			await databaseContext.Save(cancellationToken).ConfigureAwait(false);

			cookies.Append(AuthorizationCookie, newEntry.Id.ToString());

			if (request.Query.TryGetValue("state", out stringValues) && stringValues.Count > 0)
				return stringValues.First();
			return null;
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

			var currentAppTask = gitHubClientFactory.CreateAppClient().GitHubApps.GetCurrent();
			var client = await CreateInstallationClient(pullRequest.Base.Repository.Id, cancellationToken).ConfigureAwait(false);

			cancellationToken.ThrowIfCancellationRequested();

			var openComments = await client.Issue.Comment.GetAllForIssue(pullRequest.Base.Repository.Id, pullRequest.Number).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

			var currentApp = await currentAppTask.ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

			var botName = String.Format(CultureInfo.InvariantCulture, "{0}[BOT]", currentApp.Name.ToUpperInvariant());

			foreach (var I in openComments)
				if (I.User.Login.ToUpperInvariant() == botName)
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
		public async Task UpdateCheckRun(long repositoryId, long checkRunId, CheckRunUpdate checkRunUpdate, CancellationToken cancellationToken)
		{
			logger.LogTrace("Update check run {0} on repository {1}", checkRunId, repositoryId);
			var client = await CreateInstallationClient(repositoryId, cancellationToken).ConfigureAwait(false);
			await client.Check.Run.Update(repositoryId, checkRunId, checkRunUpdate).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public async Task<long> CreateCheckRun(long repositoryId, NewCheckRun initializer, CancellationToken cancellationToken)
		{
			logger.LogTrace("Create check run for ref {0} on repository {1}", initializer.HeadSha, repositoryId);
			var client = await CreateInstallationClient(repositoryId, cancellationToken).ConfigureAwait(false);
			var checkRun = await client.Check.Run.Create(repositoryId, initializer).ConfigureAwait(false);
			return checkRun.Id;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<CheckRun>> GetMatchingCheckRuns(long repositoryId, long checkSuiteId, CancellationToken cancellationToken)
		{
			var client = await CreateInstallationClient(repositoryId, cancellationToken).ConfigureAwait(false);
			var response = await client.Check.Run.GetAllForCheckSuite(repositoryId, checkSuiteId).ConfigureAwait(false);
			return response.CheckRuns;
		}
	}
}
