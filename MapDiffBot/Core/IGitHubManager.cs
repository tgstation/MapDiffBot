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
		/// <param name="repoOwner">The <see cref="Repository.Owner"/> for the operation</param>
		/// <param name="repoName">The <see cref="Repository.Name"/> for the operation</param>
		/// <param name="cookies">The <see cref="IRequestCookieCollection"/> to check</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in <see langword="true"/>, <see langword="false"/> on otherwise</returns>
		Task<bool> CheckAuthorization(string repoOwner, string repoName, IRequestCookieCollection cookies, CancellationToken cancellationToken);

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
	}
}
