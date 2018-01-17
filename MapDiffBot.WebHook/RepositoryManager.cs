using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <inheritdoc />
	sealed class RepositoryManager : IRepositoryManager
	{
		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="RepositoryManager"/>
		/// </summary>
		readonly IIOManager ioManager;

		/// <summary>
		/// <see cref="Dictionary{TKey, TValue}"/> of repoPaths to <see cref="Task"/>s that will finish when they are done being used
		/// </summary>
		readonly Dictionary<string, Task> activeRepositories;

		/// <summary>
		/// Construct a <see cref="RepositoryManager"/>
		/// </summary>
		/// <param name="_ioManager">The value of <see cref="ioManager"/></param>
		public RepositoryManager(IIOManager _ioManager)
		{
			ioManager = new ResolvingIOManager(_ioManager ?? throw new ArgumentNullException(nameof(_ioManager)), "Repositories");

			activeRepositories = new Dictionary<string, Task>();
		}

		/// <summary>
		/// Attempt to load the <see cref="IRepository"/> at <paramref name="repoPath"/>. Awaits until all <see cref="IRepository"/>'s referencing <paramref name="repoPath"/> created by <see langword="this"/> are disposed
		/// </summary>
		/// <param name="repoPath">The path to the <see cref="IRepository"/></param>
		/// <param name="token">The <see cref="CancellationToken"/> token for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the <see cref="IRepository"/> at <paramref name="repoPath"/></returns>
		async Task<IRepository> TryLoadRepository(string repoPath, CancellationToken token)
		{
			var tcs1 = new TaskCompletionSource<bool>();
			var tcs2 = new TaskCompletionSource<object>();

			lock (this)
			{
				if (activeRepositories.TryGetValue(repoPath, out Task usageTask))
					activeRepositories[repoPath] = usageTask.ContinueWith(async (t) =>
					{
						tcs1.SetResult(false);
						await tcs2.Task;
						token.ThrowIfCancellationRequested();
					});
				else
					tcs1.SetResult(true);
			}

			bool needsNewKey;
			using(token.Register(() => {
					tcs1.SetCanceled();
					tcs2.SetCanceled();
				}))
				needsNewKey = await tcs1.Task;
			token.ThrowIfCancellationRequested();
			
			var repoObj = new LibGit2Sharp.Repository(ioManager.ResolvePath(repoPath));
			token.ThrowIfCancellationRequested();

			repoObj.RemoveUntrackedFiles();
			token.ThrowIfCancellationRequested();

			var res = new Repository(repoObj, tcs2);

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
		public async Task<IRepository> GetRepository(string owner, string name, CancellationToken token)
		{
			if (owner == null)
				throw new ArgumentNullException(nameof(owner));
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			await ioManager.CreateDirectory(".", token);

			var repoPath = ioManager.ConcatPath(owner, name);

			try
			{
				return await TryLoadRepository(repoPath, token);
			}
			catch (RepositoryNotFoundException) { }

			await ioManager.DeleteDirectory(repoPath, token);
			await ioManager.CreateDirectory(repoPath, token);
			var cloneOpts = new CloneOptions
			{
				Checkout = true,
				OnProgress = (m) =>
				{
					return !token.IsCancellationRequested;
				},
				OnTransferProgress = (p) =>
				{
					return !token.IsCancellationRequested;
				}
			};

			var gitHubURL = String.Format(CultureInfo.InvariantCulture, "https://github.com/{0}/{1}", owner, name);
			await Task.Factory.StartNew(() => LibGit2Sharp.Repository.Clone(gitHubURL, ioManager.ResolvePath(repoPath), cloneOpts), token, TaskCreationOptions.LongRunning, TaskScheduler.Current);

			token.ThrowIfCancellationRequested();
			return await TryLoadRepository(repoPath, token);
		}
	}
}