using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// Manages access to a set of GitHub repositories
	/// </summary>
	interface IRepositoryManager
	{
		/// <summary>
		/// Get the GitHub <see cref="IRepository"/> that is represented by <paramref name="owner"/> and <paramref name="name"/>
		/// </summary>
		/// <param name="owner">The GitHub user that owns the <see cref="IRepository"/></param>
		/// <param name="name">The name of the <see cref="IRepository"/></param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the <see cref="IRepository"/> represented by <paramref name="owner"/> and <paramref name="name"/></returns>
		Task<IRepository> GetRepository(string owner, string name, CancellationToken token);
	}
}
