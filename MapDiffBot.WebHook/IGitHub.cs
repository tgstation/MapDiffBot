using System.Collections.Generic;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see langword="interface"/> for using the GitHub API
	/// </summary>
	interface IGitHub
	{
		/// <summary>
		/// The personal access token used to access GitHub API
		/// </summary>
		string AccessToken { set; }

		/// <summary>
		/// List paths to .dmm files changed/added/deleted by a pull request represented by <paramref name="pullRequestNumber"/> in the target <paramref name="repository"/>
		/// </summary>
		/// <param name="repository">The <see cref="Octokit.Repository"/> the pull request is from</param>
		/// <param name="pullRequestNumber">The number of the pull request</param>
		/// <returns>List paths to .dmm files changed/added/deleted by the pull request represented by <paramref name="pullRequestNumber"/> in the target <paramref name="repository"/></returns>
		Task<List<string>> GetChangedMapFiles(Octokit.Repository repository, int pullRequestNumber);

		/// <summary>
		/// Creates a comment on the specified issue, or updates it if it has already done so
		/// </summary>
		/// <param name="repository">The <see cref="Repository"/> that contains the issue</param>
		/// <param name="issueNumber">The number of the issue</param>
		/// <param name="body">The body of the comment</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task CreateSingletonComment(Octokit.Repository repository, int issueNumber, string body);

		/// <summary>
		/// Check that the provided pull request is mergeable
		/// </summary>
		/// <param name="repository">The <see cref="Octokit.Repository"/> the pull request is from</param>
		/// <param name="pullRequestNumber">The number of the pull request</param>
		/// <returns>The value of <see cref="Octokit.PullRequest.Mergeable"/> for the pull request</returns>
		Task<bool?> CheckPullRequestMergeable(Octokit.Repository repository, int pullRequestNumber);
	}
}
