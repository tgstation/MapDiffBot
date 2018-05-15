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
		void ProcessPayload(PullRequestEventPayload payload);

        /// <summary>
        /// Process a <see cref="CheckSuiteEventPayload"/>
        /// </summary>
        /// <param name="payload">The <see cref="CheckSuiteEventPayload"/> to process</param>
        void ProcessPayload(CheckSuiteEventPayload payload);

        /// <summary>
        /// Process a <see cref="CheckRunEventPayload"/>
        /// </summary>
        /// <param name="payload">The <see cref="CheckRunEventPayload"/> to process</param>
        /// <param name="gitHubManager">The <see cref="IGitHubManager"/> for the operation</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
        /// <returns>A <see cref="Task"/> representing the running operation</returns>
        Task ProcessPayload(CheckRunEventPayload payload, IGitHubManager gitHubManager, CancellationToken cancellationToken);
    }
}