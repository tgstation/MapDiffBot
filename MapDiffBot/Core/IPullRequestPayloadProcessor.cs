using Microsoft.AspNetCore.Mvc;
using Octokit;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Processes GitHub payloads
	/// </summary>
	public interface IPayloadProcessor
	{
		/// <summary>
		/// Process a <see cref="PullRequestEventPayload"/>
		/// </summary>
		/// <param name="payload">The <see cref="PullRequestEventPayload"/> to process</param>
		void ProcessPayload(PullRequestEventPayload payload);

		/// <summary>
		/// Process a <see cref="IssueCommentPayload"/>
		/// </summary>
		/// <param name="payload">The <see cref="IssueCommentPayload"/> to process</param>
		void ProcessPayload(IssueCommentPayload payload);
	}
}