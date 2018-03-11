using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
#pragma warning disable CA1812
	sealed class LocalRepositoryManager : ILocalRepositoryManager
#pragma warning restore CA1812
	{
		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="LocalRepositoryManager"/>
		/// </summary>
		readonly IIOManager ioManager;

		/// <summary>
		/// <see cref="Dictionary{TKey, TValue}"/> of repoPaths to <see cref="Task"/>s that will finish when they are done being used
		/// </summary>
		readonly Dictionary<string, Task> activeRepositories;

		/// <summary>
		/// Construct a <see cref="LocalRepositoryManager"/>
		/// </summary>
		/// <param name="ioManager">The value of <see cref="ioManager"/></param>
		public LocalRepositoryManager(IIOManager ioManager)
		{
			this.ioManager = new ResolvingIOManager(ioManager ?? throw new ArgumentNullException(nameof(ioManager)), "Repositories");
			activeRepositories = new Dictionary<string, Task>();
		}

		/// <summary>
		/// Creates a <see cref="ILocalRepository"/>
		/// </summary>
		/// <param name="repoPath">The path to the <see cref="ILocalRepository"/></param>
		/// <param name="usageTask">The <see cref="TaskCompletionSource{TResult}"/> that indicates the lifetime of the resulting <see cref="ILocalRepository"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> token for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the <see cref="ILocalRepository"/> at <paramref name="repoPath"/></returns>
		async Task<ILocalRepository> CreateRepositoryObject(string repoPath, TaskCompletionSource<object> usageTask, CancellationToken cancellationToken)
		{
			Repository repoLib = null;
			await Task.Factory.StartNew(() =>
			{
				repoLib = new Repository(ioManager.ResolvePath(repoPath));
				cancellationToken.ThrowIfCancellationRequested();
				repoLib.RemoveUntrackedFiles();
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
			return new LocalRepository(repoLib, usageTask);
		}

		/// <summary>
		/// Attempt to load the <see cref="ILocalRepository"/> at <paramref name="repoPath"/>. Awaits until all <see cref="ILocalRepository"/>'s referencing <paramref name="repoPath"/> created by <see langword="this"/> are disposed
		/// </summary>
		/// <param name="repoPath">The path to the <see cref="ILocalRepository"/></param>
		/// <param name="onOperationBlocked">A <see cref="Func{TResult}"/> returning a <see cref="Task"/> to run if the operation is blocked by a repository lock</param>
		/// <param name="recieveUsageTask">The <see cref="Action"/> with the current <see cref="TaskCompletionSource{TResult}"/> for <see cref="activeRepositories"/> to run if a clone is required</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> token for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the <see cref="ILocalRepository"/> at <paramref name="repoPath"/></returns>
		async Task<ILocalRepository> TryLoadRepository(string repoPath, Func<Task> onOperationBlocked, Action<TaskCompletionSource<object>> recieveUsageTask, CancellationToken cancellationToken)
		{
			var tcs1 = new TaskCompletionSource<bool>();
			var tcs2 = new TaskCompletionSource<object>();
			Task usageTask = null;

			lock (activeRepositories)
			{
				if (activeRepositories.TryGetValue(repoPath, out usageTask))
					activeRepositories[repoPath] = usageTask.ContinueWith(async (t) =>
					{
						tcs1.SetResult(false);
						try
						{
							await tcs2.Task.ConfigureAwait(false);
						}
						catch (OperationCanceledException) { }
					}, cancellationToken, TaskContinuationOptions.None, TaskScheduler.Current);
				else
					tcs1.SetResult(true);
			}

			Task operationBlockedTask = null;
			if (usageTask != null && onOperationBlocked != null)
				operationBlockedTask = onOperationBlocked();

			bool needsNewKey;
			try
			{
				using (cancellationToken.Register(() =>
				{
					tcs1.SetCanceled();
					tcs2.SetCanceled();
				}))
					needsNewKey = await tcs1.Task.ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();
			}
			finally
			{
				if (operationBlockedTask != null)
					await operationBlockedTask.ConfigureAwait(false);
			}

			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				return await CreateRepositoryObject(repoPath, tcs2, cancellationToken).ConfigureAwait(false);
			}
			catch (LibGit2SharpException)
			{
				recieveUsageTask(tcs2);
				throw;
			}
			finally
			{
				lock (this)
				{
					if (needsNewKey)
						activeRepositories.Add(repoPath, tcs2.Task);
					else
						activeRepositories[repoPath] = tcs2.Task;
				}
			}
		}
	
		/// <inheritdoc />
		public async Task<ILocalRepository> GetRepository(Octokit.Repository repository, Func<int, Task> onCloneProgress, Func<Task> onOperationBlocked, CancellationToken cancellationToken)
		{
			if (repository == null)
				throw new ArgumentNullException(nameof(repository));

			await ioManager.CreateDirectory(".", cancellationToken).ConfigureAwait(false);

			var repoPath = ioManager.ConcatPath(repository.Owner.Login, repository.Name);

			TaskCompletionSource<object> usageTask = null;
			try
			{
				return await TryLoadRepository(repoPath, onOperationBlocked, tcs => usageTask = tcs, cancellationToken).ConfigureAwait(false);
			}
			catch (LibGit2SharpException) { }

			try
			{
				List<Task> cloneTasks = null;
				if(onCloneProgress != null)
					cloneTasks = new List<Task>() { onCloneProgress(0) };

				await ioManager.DeleteDirectory(repoPath, cancellationToken).ConfigureAwait(false);
				await ioManager.CreateDirectory(repoPath, cancellationToken).ConfigureAwait(false);

				var gitHubURL = String.Format(CultureInfo.InvariantCulture, "https://github.com/{0}/{1}", repository.Owner.Login, repository.Name);
				await Task.Factory.StartNew(() =>
				{
					try
					{
						Repository.Clone(gitHubURL, ioManager.ResolvePath(repoPath), new CloneOptions
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

				var result = await CreateRepositoryObject(repoPath, usageTask, cancellationToken).ConfigureAwait(false);
				if(cloneTasks != null)
					await Task.WhenAll(cloneTasks).ConfigureAwait(false);
				return result;
			}
			catch
			{
				usageTask.SetResult(null);
				throw;
			}
		}
	}
}