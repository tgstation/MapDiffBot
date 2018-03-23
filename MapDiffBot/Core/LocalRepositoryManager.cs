using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
		/// The <see cref="ILocalRepositoryFactory"/> for the <see cref="LocalRepositoryManager"/>
		/// </summary>
		readonly ILocalRepositoryFactory localRepositoryFactory;
		/// <summary>
		/// The <see cref="IRepositoryOperations"/> for the <see cref="LocalRepositoryManager"/>
		/// </summary>
		readonly IRepositoryOperations repositoryOperations;
		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="LocalRepositoryManager"/>
		/// </summary>
		readonly ILogger<LocalRepositoryManager> logger;

		/// <summary>
		/// <see cref="Dictionary{TKey, TValue}"/> of repoPaths to <see cref="Task"/>s that will finish when they are done being used
		/// </summary>
		readonly Dictionary<string, Task> activeRepositories;

		/// <summary>
		/// Construct a <see cref="LocalRepositoryManager"/>
		/// </summary>
		/// <param name="ioManager">The value of <see cref="ioManager"/></param>
		/// <param name="localRepositoryFactory">The value of <see cref="localRepositoryFactory"/></param>
		/// <param name="repositoryOperations">The value of <see cref="repositoryOperations"/></param>
		/// <param name="logger">The value of <see cref="logger"/></param>
		public LocalRepositoryManager(IIOManager ioManager, ILocalRepositoryFactory localRepositoryFactory, IRepositoryOperations repositoryOperations, ILogger<LocalRepositoryManager> logger)
		{
			this.ioManager = new ResolvingIOManager(ioManager ?? throw new ArgumentNullException(nameof(ioManager)), "Repositories");
			this.localRepositoryFactory = localRepositoryFactory ?? throw new ArgumentNullException(nameof(localRepositoryFactory));
			this.repositoryOperations = repositoryOperations ?? throw new ArgumentNullException(nameof(repositoryOperations));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			activeRepositories = new Dictionary<string, Task>();
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

			Task usageTask;
			async Task Continuation()
			{
				//first wait our turn
				await usageTask.ConfigureAwait(false);
				//let the function know it can continue
				repoBusyTask.SetResult(null);
				try
				{
					//then wait for it's ourRepoUsageTask to complete
					await ourRepoUsageTask.Task.ConfigureAwait(false);
				}
				catch (OperationCanceledException) { }
			};

			lock (activeRepositories)
			{
				if (!activeRepositories.TryGetValue(repoPath, out usageTask))
					//set it to completed task to allow for consistency
					usageTask = Task.CompletedTask;
				operationBlocked = !usageTask.IsCompleted;

				activeRepositories[repoPath] = Continuation();
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
				return await localRepositoryFactory.CreateLocalRepository(ioManager.ResolvePath(repoPath), ourRepoUsageTask, cancellationToken).ConfigureAwait(false);
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
			catch (LibGit2SharpException e)
			{
				logger.LogWarning(e, "Failed to load repository {0}/{1}! Cloning...", repository.Owner.Login, repository.Name);
			}

			//so the repo failed to load and now we're holding our queue spot in usageTask
			//reclone it
			try
			{
				await ioManager.DeleteDirectory(repoPath, cancellationToken).ConfigureAwait(false);
				await ioManager.CreateDirectory(repoPath, cancellationToken).ConfigureAwait(false);
				await repositoryOperations.Clone(repository.CloneUrl, ioManager.ResolvePath(repoPath), onCloneProgress, cancellationToken).ConfigureAwait(false);
				return await localRepositoryFactory.CreateLocalRepository(ioManager.ResolvePath(repoPath), usageTask, cancellationToken).ConfigureAwait(false);
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