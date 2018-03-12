using Octokit;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Constructs <see cref="IGitHubClient"/>s
	/// </summary>
    interface IGitHubClientFactory
	{
		/// <summary>
		/// Create a <see cref="GitHubApp"/> level <see cref="IGitHubClient"/>
		/// </summary>
		/// <returns>A new <see cref="IGitHubClient"/> valid for only 60s</returns>
		IGitHubClient CreateAppClient();

		/// <summary>
		/// Create a <see cref="IGitHubClient"/> based on an <paramref name="accessToken"/>
		/// </summary>
		/// <param name="accessToken">The oauth access token</param>
		/// <returns>A new <see cref="IGitHubClient"/></returns>
		IGitHubClient CreateOauthClient(string accessToken);

		//TODO remove this
		/// <summary>
		/// Get a list of <see cref="Repository"/>s in an <see cref="Installation"/>
		/// </summary>
		/// <param name="installationToken">The installation oauth token</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in a <see cref="IReadOnlyList{T}"/> of <see cref="Repository"/>s in the <see cref="Installation"/></returns>
		Task<IReadOnlyList<Repository>> GetInstallationRepositories(string installationToken, CancellationToken cancellationToken);
	}
}
