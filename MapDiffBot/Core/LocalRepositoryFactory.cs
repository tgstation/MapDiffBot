using LibGit2Sharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
	sealed class LocalRepositoryFactory : ILocalRepositoryFactory
	{
		/// <summary>
		/// The <see cref="IRepositoryOperations"/> for the <see cref="LocalRepositoryFactory"/>
		/// </summary>
		readonly IRepositoryOperations repositoryOperations;

		/// <summary>
		/// Construct a <see cref="LocalRepositoryFactory"/>
		/// </summary>
		/// <param name="reposiotoryOperations">The value of <see cref="repositoryOperations"/></param>
		public LocalRepositoryFactory(IRepositoryOperations reposiotoryOperations) => this.repositoryOperations = reposiotoryOperations ?? throw new ArgumentNullException(nameof(reposiotoryOperations));

		/// <inheritdoc />
		public Task<ILocalRepository> CreateLocalRepository(string path, TaskCompletionSource<object> onDisposal, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			if (onDisposal == null)
				throw new ArgumentNullException(nameof(onDisposal));
			var repo = new Repository(path);
			cancellationToken.ThrowIfCancellationRequested();
			repo.RemoveUntrackedFiles();
			return (ILocalRepository)new LocalRepository(repo, repositoryOperations, onDisposal);
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
	}
}
