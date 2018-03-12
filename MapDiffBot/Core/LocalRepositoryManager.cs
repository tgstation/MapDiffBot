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
			//this function is a little complicated but it ensurse only one using of a repo at a time

			//what happens is a queue of tasks is created via ContinueWith
			//every call to this function will insert it's own task into the queue and then block until it's run
			//then it'll either return a ILocalRepository which will determine when that task ends or call recieveUsageTask
			//either way it's up to the caller to complete the task, and thus, allowing the queue to proceed
			
			//represents the thing in front of us in the queue
			var repoBusyTask = new TaskCompletionSource<object>();
			//represents our spot in the queue
			var ourRepoUsageTask = new TaskCompletionSource<object>();

			//if we weren't the very first person in the queue
			bool operationBlocked;

			lock (activeRepositories)
			{
				operationBlocked = activeRepositories.TryGetValue(repoPath, out Task usageTask);
				if (!operationBlocked)
					//set it to completed task to allow for consistency
					usageTask = Task.CompletedTask;
				activeRepositories[repoPath] = usageTask.ContinueWith(async (t) =>
				{
					//first let this function know it's now its turn
					repoBusyTask.SetResult(null);
					try
					{
						//then wait for it's ourRepoUsageTask to complete
						await ourRepoUsageTask.Task.ConfigureAwait(false);
					}
					catch (OperationCanceledException) { }
				}, cancellationToken, TaskContinuationOptions.None, TaskScheduler.Current);
			}

			Task operationBlockedTask = null;
			if (operationBlocked && onOperationBlocked != null)
				operationBlockedTask = onOperationBlocked();
			
			try
			{
				using (cancellationToken.Register(() =>
				{
					repoBusyTask.SetCanceled();
					ourRepoUsageTask.SetCanceled();
				}))
					//wait for our turn
					await repoBusyTask.Task.ConfigureAwait(false);
			}
			finally
			{
				if (operationBlockedTask != null)
					await operationBlockedTask.ConfigureAwait(false);
			}

			//ourRepoUsageTask is now the front of the queue
			//pass it on
			try
			{
				return await CreateRepositoryObject(repoPath, ourRepoUsageTask, cancellationToken).ConfigureAwait(false);
			}
			catch (LibGit2SharpException)
			{
				recieveUsageTask(ourRepoUsageTask);
				throw;
			}
		}
	
		/// <inheritdoc />
		public async Task<ILocalRepository> GetRepository(Octokit.Repository repository, Func<int, Task> onCloneProgress, Func<Task> onOperationBlocked, CancellationToken cancellationToken)
		{
			if (repository == null)
				throw new ArgumentNullException(nameof(repository));

			var repoPath = ioManager.ConcatPath(repository.Owner.Login, repository.Name);

			TaskCompletionSource<object> usageTask = null;
			try
			{
				return await TryLoadRepository(repoPath, onOperationBlocked, tcs => usageTask = tcs, cancellationToken).ConfigureAwait(false);
			}
			catch (LibGit2SharpException) { }

			//so the repo failed to load and now we're holding our queue spot in usageTask
			//reclone it
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
				//ok we can't do anything else, clear our queue spot
				usageTask.SetResult(null);
				throw;
			}
		}
	}
}