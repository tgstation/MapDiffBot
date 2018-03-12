using Microsoft.AspNetCore.Mvc;
using Octokit;
using System.Threading;
using System.Threading.Tasks;

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
		/// <param name="urlHelper">The <see cref="IUrlHelper"/> for the operation</param>
		void ProcessPayload(PullRequestEventPayload payload, IUrlHelper urlHelper);

		/// <summary>
		/// Process a <see cref="IssueCommentPayload"/>
		/// </summary>
		/// <param name="payload">The <see cref="IssueCommentPayload"/> to process</param>
		/// <param name="urlHelper">The <see cref="IUrlHelper"/> for the operation</param>
		void ProcessPayload(IssueCommentPayload payload, IUrlHelper urlHelper);
	}
}