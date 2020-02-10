using Microsoft.AspNetCore.Http;
using Octokit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Manages operations with GitHub.com
	/// </summary>
	public interface IGitHubManager
	{
		/// <summary>
		/// Get the GitHub URL to direct a user to at the start of the Oauth flow
		/// </summary>
		/// <param name="callbackURL">The <see cref="Uri"/> to direct users to to complete the Oauth flow</param>
		/// <param name="state">A <see cref="string"/> to be passed into the query parameters of <paramref name="callbackURL"/> when the use visits it with the code</param>
		/// <returns>The <see cref="Uri"/> to send the user to</returns>
		Uri GetAuthorizationURL(Uri callbackURL, string state);

		/// <summary>
		/// Complete the Oauth flow
		/// </summary>
		/// <param name="request">The web request</param>
		/// <param name="cookies">The <see cref="IResponseCookies"/> to write session information to</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the value of the state parameter of <see cref="GetAuthorizationURL(Uri, string)"/> on success, <see langword="null"/> on failure</returns>
		Task<string> CompleteAuthorization(HttpRequest request, IResponseCookies cookies, CancellationToken cancellationToken);

		/// <summary>
		/// Expire an oauth cookie
		/// </summary>
		/// <param name="cookies">The <see cref="IResponseCookies"/> to write session information to</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		void ExpireAuthorization(IResponseCookies cookies);

		/// <summary>
		/// Checks some <paramref name="cookies"/> for the oauth cookie
		/// </summary>
		/// <param name="repositoryId">The <see cref="Repository.Id"/> for the operation</param>
		/// <param name="cookies">The <see cref="IRequestCookieCollection"/> to check</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="AuthenticationLevel"/> of the user</returns>
		Task<AuthenticationLevel> CheckAuthorization(long repositoryId, IRequestCookieCollection cookies, CancellationToken cancellationToken);

		/// <summary>
		/// Creates a comment on a given <paramref name="pullRequest"/>, or updates the first one if it has already done so
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/> to comment on</param>
		/// <param name="body">The body of the comment</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task CreateSingletonComment(PullRequest pullRequest, string body, CancellationToken cancellationToken);

		/// <summary>
		/// Gets a <see cref="PullRequest"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="Repository.Id"/> of the <see cref="PullRequest.Base"/></param>
		/// <param name="pullRequestNumber">The <see cref="PullRequest.Number"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in a <see cref="PullRequest"/></returns>
		Task<PullRequest> GetPullRequest(long repositoryId, int pullRequestNumber, CancellationToken cancellationToken);

		/// <summary>
		/// Get the files changed by a <see cref="PullRequest"/>
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in a <see cref="IReadOnlyList{T}"/> of <see cref="PullRequestFile"/>s</returns>
		Task<IReadOnlyList<PullRequestFile>> GetPullRequestChangedFiles(PullRequest pullRequest, CancellationToken cancellationToken);

		/// <summary>
		/// Updates <see cref="Models.IDatabaseContext.Installations"/> for the given <paramref name="repositoryId"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="Repository.Id"/> to load</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task LoadInstallation(long repositoryId, CancellationToken cancellationToken);

		/// <summary>
		/// Update a <see cref="CheckRun"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="Repository.Id"/></param>
		/// <param name="checkRunId">The <see cref="CheckRun.Id"/></param>
		/// <param name="checkRunUpdate">The <see cref="CheckRunUpdate"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task UpdateCheckRun(long repositoryId, long checkRunId, CheckRunUpdate checkRunUpdate, CancellationToken cancellationToken);

		/// <summary>
		/// Create a <see cref="CheckRun"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="Repository.Id"/></param>
		/// <param name="newCheckRun">The <see cref="NewCheckRun"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the new <see cref="CheckRun.Id"/></returns>
		Task<long> CreateCheckRun(long repositoryId, NewCheckRun newCheckRun, CancellationToken cancellationToken);

		/// <summary>
		/// Get <see cref="CheckRun"/>s matching a <paramref name="checkSuiteId"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="Repository.Id"/></param>
		/// <param name="checkSuiteId">The <see cref="CheckSuite.Id"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in an <see cref="IEnumerable{T}"/> of relevant <see cref="CheckRun"/>s</returns>
		Task<CheckRunsResponse> GetMatchingCheckRuns(long repositoryId, long checkSuiteId, CancellationToken cancellationToken);
	}
}
