using Octokit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Manages access to a set of GitHub repositories
	/// </summary>
	interface ILocalRepositoryManager
	{
		/// <summary>
		/// Get the GitHub <see cref="ILocalRepository"/> that is represented by <paramref name="repository"/>
		/// </summary>
		/// <param name="repository">The <see cref="Repository"/> to load</param>
		/// <param name="onCloneRequired">A <see cref="Func{TResult}"/> to run if this results in a cloning</param>
		/// <param name="onOperationBlocked">A <see cref="Func{TResult}"/> to run if this results in a wait on the usage of the same repository</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the <see cref="ILocalRepository"/> represented by <paramref name="repository"/></returns>
		Task<ILocalRepository> GetRepository(Repository repository, Func<Task> onCloneRequired, Func<Task> onOperationBlocked, CancellationToken cancellationToken);
	}
}
