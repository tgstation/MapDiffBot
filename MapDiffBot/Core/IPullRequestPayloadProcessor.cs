using Octokit;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Processes GitHub payloads
	/// </summary>
	public interface IPayloadProcessor
	{
		/// <summary>
		/// Process a <see cref="PullRequest"/>
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/> to process</param>
		void ProcessPullRequest(PullRequest pullRequest);

		/// <summary>
		/// Process a <see cref="IssueCommentPayload"/>
		/// </summary>
		/// <param name="payload">The <see cref="IssueCommentPayload"/> to process</param>
		void ProcessPayload(IssueCommentPayload payload);
	}
}