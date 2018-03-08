using Microsoft.AspNetCore.Http;
using Octokit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot
{
    interface IGitHubManager
	{
		/// <summary>
		/// Get the GitHub URL to direct a user to at the start of the Oauth flow
		/// </summary>
		/// <param name="callbackURL">The <see cref="Uri"/> to direct users to to complete the Oauth flow</param>
		/// <param name="repoOwner">The <see cref="Repository.Owner"/> for the operation</param>
		/// <param name="repoName">The <see cref="Repository.Name"/> for the operation</param>
		/// <param name="number">The <see cref="PullRequest.Number"/> to return to on the redirect</param>
		/// <returns>The <see cref="Uri"/> to send the user to</returns>
		Uri GetAuthorizationURL(Uri callbackURL, string repoOwner, string repoName, int number);

		/// <summary>
		/// Complete the Oauth flow and load 
		/// </summary>
		/// <param name="code">The code entry in the recieved JSON from an Oauth redirect</param>
		/// <param name="cookies">The <see cref="IResponseCookies"/> to write session information to</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task CompleteAuthorization(string code, IResponseCookies cookies, CancellationToken cancellationToken);

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
	}
}
