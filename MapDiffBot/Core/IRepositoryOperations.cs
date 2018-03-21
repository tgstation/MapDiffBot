using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <summary>
	/// For performing operations on <see cref="IRepository"/>s
	/// </summary>
    interface IRepositoryOperations
	{
		/// <summary>
		/// Fetch the origin of a <paramref name="repository"/>
		/// </summary>
		/// <param name="repository">The <see cref="IRepository"/> to fetch</param>
		/// <param name="remote">The <see cref="Remote.Name"/> to fetch</param>
		/// <param name="refSpecs">The refspecs to fetch</param>
		/// <param name="logMessage">The git log message for the operation</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Fetch(IRepository repository, string remote, IEnumerable<string> refSpecs, string logMessage, CancellationToken cancellationToken);

		/// <summary>
		/// Clone a <see cref="Repository"/>
		/// </summary>
		/// <param name="url">The URL of the <see cref="Repository"/> to clone</param>
		/// <param name="path">The path to clone the <see cref="Repository"/> to</param>
		/// <param name="onCloneProgress">A <see cref="Func{T1, TResult}"/> to run if this results in a cloning. Will be called for every status update. The parameter is a <see cref="int"/> that ranges from 0 - 100 representing progress</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Clone(string url, string path, Func<int, Task> onCloneProgress, CancellationToken cancellationToken);
	}
}
