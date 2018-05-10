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
		void ProcessCheckSuite(CheckSuite checkSuite, bool rerequest);
	}
}