﻿using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
	sealed class LocalRepositoryManager : ILocalRepositoryManager
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
		/// Attempt to load the <see cref="ILocalRepository"/> at <paramref name="repoPath"/>. Awaits until all <see cref="ILocalRepository"/>'s referencing <paramref name="repoPath"/> created by <see langword="this"/> are disposed
		/// </summary>
		/// <param name="repoPath">The path to the <see cref="ILocalRepository"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> token for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the <see cref="ILocalRepository"/> at <paramref name="repoPath"/></returns>
		async Task<ILocalRepository> TryLoadRepository(string repoPath, CancellationToken cancellationToken)
		{
			var tcs1 = new TaskCompletionSource<bool>();
			var tcs2 = new TaskCompletionSource<object>();

			lock (activeRepositories)
			{
				if (activeRepositories.TryGetValue(repoPath, out Task usageTask))
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

			bool needsNewKey;
			using(cancellationToken.Register(() => {
					tcs1.SetCanceled();
					tcs2.SetCanceled();
				}))
				needsNewKey = await tcs1.Task.ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();

			Repository repoObj = null;
			await Task.Factory.StartNew(() =>
			{
				repoObj = new Repository(ioManager.ResolvePath(repoPath));
				cancellationToken.ThrowIfCancellationRequested();
				repoObj.RemoveUntrackedFiles();
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);

			var res = new LocalRepository(repoObj, tcs2);

			lock (this)
			{
				if (needsNewKey)
					activeRepositories.Add(repoPath, tcs2.Task);
				else
					activeRepositories[repoPath] = tcs2.Task;
				return res;
			}
		}
	
		/// <inheritdoc />
		public async Task<ILocalRepository> GetRepository(Octokit.Repository repository, Func<Task> onCloneRequired, CancellationToken cancellationToken)
		{
			if (repository == null)
				throw new ArgumentNullException(nameof(repository));

			await ioManager.CreateDirectory(".", cancellationToken).ConfigureAwait(false);

			var repoPath = ioManager.ConcatPath(repository.Owner.Login, repository.Name);

			try
			{
				return await TryLoadRepository(repoPath, cancellationToken).ConfigureAwait(false);
			}
			catch (RepositoryNotFoundException) { }

			var cloneRequiredTask = onCloneRequired?.Invoke();

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
						OnTransferProgress = (p) => !cancellationToken.IsCancellationRequested,
						OnUpdateTips = (a, b, c) => !cancellationToken.IsCancellationRequested
					});
				}
				catch (UserCancelledException)
				{
					cancellationToken.ThrowIfCancellationRequested();
				}
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
			
			var result = await TryLoadRepository(repoPath, cancellationToken).ConfigureAwait(false);
			if (cloneRequiredTask != null)
				await cloneRequiredTask.ConfigureAwait(false);
			return result;
		}
	}
}