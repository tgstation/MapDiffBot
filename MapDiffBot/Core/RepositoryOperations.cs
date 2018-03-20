using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
	sealed class RepositoryOperations : IRepositoryOperations
	{
		/// <inheritdoc />
		public Task Fetch(IRepository repository, string remote, IEnumerable<string> refspecs, string logMessage, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			try
			{
				Commands.Fetch(repository as Repository, remote, refspecs, new FetchOptions()
				{
					OnProgress = (m) => {
						return !cancellationToken.IsCancellationRequested;
					},
					OnTransferProgress = (p) => {
						return !cancellationToken.IsCancellationRequested;
					},
					Prune = true,
					TagFetchMode = TagFetchMode.Auto
				}, logMessage);
			}
			catch (UserCancelledException)
			{
				cancellationToken.ThrowIfCancellationRequested();
			}
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

/// <inheritdoc />
public async Task Clone(string url, string path, Func<int, Task> onCloneProgress, CancellationToken cancellationToken)
		{
			if (url == null)
				throw new ArgumentNullException(nameof(url));
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			List<Task> cloneTasks = null;
			if (onCloneProgress != null)
				cloneTasks = new List<Task>() { onCloneProgress(0) };
			await Task.Factory.StartNew(() =>
			{
				try
				{
					Repository.Clone(url, path, new CloneOptions
					{
						Checkout = true,
						OnProgress = (m) => !cancellationToken.IsCancellationRequested,
						OnTransferProgress = (transferProgress) =>
						{
							if (cancellationToken.IsCancellationRequested)
								return false;
							if (cloneTasks != null)
							{
								var newTask = onCloneProgress((int)Math.Floor((100.0 * (transferProgress.ReceivedObjects + transferProgress.IndexedObjects)) / (transferProgress.TotalObjects * 2)));
								if (newTask != null)
									cloneTasks.Add(newTask);
							}
							return true;
						},
						OnUpdateTips = (a, b, c) => !cancellationToken.IsCancellationRequested
					});
				}
				catch (UserCancelledException)
				{
					cancellationToken.ThrowIfCancellationRequested();
				}
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
			if (cloneTasks != null)
				await Task.WhenAll(cloneTasks).ConfigureAwait(false);
		}
	}
}
