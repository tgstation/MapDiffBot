using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
	sealed class LocalRepository : ILocalRepository
	{
		/// <summary>
		/// Creates <see cref="FetchOptions"/> used by the <see cref="LocalRepository"/>
		/// </summary>
		/// <param name="cancellationToken"><see cref="CancellationToken"/> for the fetch these <see cref="FetchOptions"/> will be used in</param>
		/// <returns><see cref="FetchOptions"/> used by the <see cref="LocalRepository"/></returns>
		static FetchOptions GenerateFetchOptions(CancellationToken cancellationToken)
		{
			return new FetchOptions()
			{
				OnProgress = (m) => {
					return !cancellationToken.IsCancellationRequested;
				},
				OnTransferProgress = (p) => {
					return !cancellationToken.IsCancellationRequested;
				},
				Prune = true,
				TagFetchMode = TagFetchMode.Auto
			};
		}

		/// <inheritdoc />
		public string Path => repositoryLib.Info.WorkingDirectory;

		/// <summary>
		/// The backing <see cref="Repository"/>
		/// </summary>
		readonly Repository repositoryLib;
		/// <summary>
		/// The <see cref="TaskCompletionSource{TResult}"/> that is completed when <see cref="Dispose"/> is called
		/// </summary>
		readonly TaskCompletionSource<object> onDisposal;

		/// <summary>
		/// If <see cref="Dispose"/> was called
		/// </summary>
		bool disposed;

		/// <summary>
		/// Construct a <see cref="LocalRepository"/>
		/// </summary>
		/// <param name="repositoryLib">The value of <see cref="repositoryLib"/></param>
		/// <param name="onDisposal">The value of <see cref="onDisposal"/></param>
		public LocalRepository(Repository repositoryLib, TaskCompletionSource<object> onDisposal)
		{
			this.repositoryLib = repositoryLib ?? throw new ArgumentNullException(nameof(repositoryLib));
			this.onDisposal = onDisposal ?? throw new ArgumentNullException(nameof(onDisposal));
		}

		/// <summary>
		/// Finalized the <see cref="LocalRepository"/>
		/// </summary>
		~LocalRepository() => Dispose();

		/// <summary>
		/// Disposes the <see cref="LocalRepository"/> and completes <see cref="onDisposal"/>
		/// </summary>
		public void Dispose()
		{
			if (disposed)
				return;
			repositoryLib.Dispose();
			onDisposal.SetResult(null);
			disposed = true;
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc />
		public Task Checkout(string commitish, CancellationToken cancellationToken) => Task.Factory.StartNew(() => Commands.Checkout(repositoryLib, commitish ?? throw new ArgumentNullException(nameof(commitish))), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task<bool> ContainsCommit(string sha, CancellationToken cancellationToken) => Task.Factory.StartNew(() => repositoryLib.Lookup(sha) != null, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task Fetch(CancellationToken token) => Task.Factory.StartNew(() =>
			{
				var remote = repositoryLib.Network.Remotes.First();
				var refSpecs = remote.FetchRefSpecs.Select(X => X.Specification);
				Commands.Fetch(repositoryLib, remote.Name, refSpecs, GenerateFetchOptions(token), "Update of origin branch");
				token.ThrowIfCancellationRequested();
			}, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task FetchPullRequest(int prNumber, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
			{
				if (prNumber < 1)
					throw new ArgumentOutOfRangeException(nameof(prNumber), prNumber, String.Format(CultureInfo.CurrentCulture, "{0} must be greater than zero!", nameof(prNumber)));
				var remote = repositoryLib.Network.Remotes.First();
				var prBranchName = String.Format(CultureInfo.InvariantCulture, "pr-{0}", prNumber);
				var refSpecs = new List<string>
				{
					String.Format(CultureInfo.InvariantCulture, "pull/{0}/head:{1}", prNumber, prBranchName)
				};
				Commands.Fetch(repositoryLib, remote.Name, refSpecs, GenerateFetchOptions(cancellationToken), String.Format(CultureInfo.CurrentCulture, "Fetch of pull request #{0}", prNumber));
				cancellationToken.ThrowIfCancellationRequested();
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task Merge(string commitish, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
			{
				if (commitish == null)
					throw new ArgumentNullException(nameof(commitish));
				if (repositoryLib.Merge(commitish, new Signature(new Identity(nameof(MapDiffBot), String.Format(CultureInfo.InvariantCulture, "{0}@users.noreply.github.com", nameof(MapDiffBot))), DateTime.UtcNow)).Status == MergeStatus.Conflicts)
				{
					repositoryLib.Reset(ResetMode.Hard);
					throw new InvalidOperationException("The resulting merge has conflicts!");
				}
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
	}
}