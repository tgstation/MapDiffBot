using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// A local git repository
	/// </summary>
	interface IRepository : IDisposable
	{
		/// <summary>
		/// The path to the <see cref="IRepository"/>
		/// </summary>
		string Path { get; }

		/// <summary>
		/// Check if a commit exists in the <see cref="IRepository"/>
		/// </summary>
		/// <param name="sha">The SHA of the commit to lookup</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in <see langword="true"/> if <paramref name="sha"/> exists in the <see cref="IRepository"/>, <see langword="false"/> otherwise</returns>
		Task<bool> ContainsCommit(string sha, CancellationToken token);

		/// <summary>
		/// Switches the <see cref="IRepository"/> HEAD to the specified <paramref name="commitish"/>
		/// </summary>
		/// <param name="commitish">The name of the commit pointing object to switch to</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Checkout(string commitish, CancellationToken token);

		/// <summary>
		/// Fetches the origin remote of the <see cref="IRepository"/>
		/// </summary>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Fetch(CancellationToken token);

		/// <summary>
		/// Fetch commits from a GitHub pull request into the <see cref="IRepository"/>
		/// </summary>
		/// <param name="prNumber">The number of the pull request</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task FetchPullRequest(int prNumber, CancellationToken token);
	}
}
