using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <summary>
	/// For creating <see cref="ILocalRepository"/>s
	/// </summary>
	interface ILocalRepositoryFactory
	{
		/// <summary>
		/// Create a <see cref="ILocalRepository"/>
		/// </summary>
		/// <param name="path">The path of the <see cref="ILocalRepository"/></param>
		/// <param name="onDisposal">The <see cref="TaskCompletionSource{TResult}"/> to complete when the <see cref="ILocalRepository"/> is <see cref="System.IDisposable.Dispose"/>d</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in a new <see cref="ILocalRepository"/></returns>
		Task<ILocalRepository> CreateLocalRepository(string path, TaskCompletionSource<object> onDisposal, CancellationToken cancellationToken);
	}
}
