using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <summary>
	/// A local git repository
	/// </summary>
	interface ILocalRepository : IDisposable
	{
		/// <summary>
		/// The path to the <see cref="ILocalRepository"/>
		/// </summary>
		string Path { get; }

		/// <summary>
		/// Check if a commit exists in the <see cref="ILocalRepository"/>
		/// </summary>
		/// <param name="sha">The SHA of the commit to lookup</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in <see langword="true"/> if <paramref name="sha"/> exists in the <see cref="ILocalRepository"/>, <see langword="false"/> otherwise</returns>
		Task<bool> ContainsCommit(string sha, CancellationToken cancellationToken);

		/// <summary>
		/// Switches the <see cref="ILocalRepository"/> HEAD to the specified <paramref name="commitish"/>
		/// </summary>
		/// <param name="commitish">The name of the commit pointing object to switch to</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Checkout(string commitish, CancellationToken cancellationToken);

		/// <summary>
		/// Fetches the origin remote of the <see cref="ILocalRepository"/>
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Fetch(CancellationToken cancellationToken);

		/// <summary>
		/// Fetch commits from a GitHub pull request into the <see cref="ILocalRepository"/>
		/// </summary>
		/// <param name="prNumber">The number of the pull request</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task FetchPullRequest(int prNumber, CancellationToken cancellationToken);

		/// <summary>
		/// Attempt to merge <paramref name="commitish"/> into the current HEAD
		/// </summary>
		/// <param name="commitish">The name of the commit pointing object to merge to</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Merge(string commitish, CancellationToken cancellationToken);
	}
}
