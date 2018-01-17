using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <inheritdoc />
	sealed class Repository : IRepository
	{
		/// <summary>
		/// Creates <see cref="FetchOptions"/> used by the <see cref="Repository"/>
		/// </summary>
		/// <param name="token"><see cref="CancellationToken"/> for the fetch these <see cref="FetchOptions"/> will be used in</param>
		/// <returns><see cref="FetchOptions"/> used by the <see cref="Repository"/></returns>
		static FetchOptions GenerateFetchOptions(CancellationToken token)
		{
			return new FetchOptions()
			{
				OnProgress = (m) => {
					return !token.IsCancellationRequested;
				},
				OnTransferProgress = (p) => {
					return !token.IsCancellationRequested;
				},
				Prune = true,
				TagFetchMode = TagFetchMode.Auto
			};
		}

		/// <inheritdoc />
		public string Path => repositoryLib.Info.WorkingDirectory;

		/// <summary>
		/// The backing <see cref="LibGit2Sharp.Repository"/>
		/// </summary>
		readonly LibGit2Sharp.Repository repositoryLib;
		/// <summary>
		/// The <see cref="TaskCompletionSource{TResult}"/> that is completed when <see cref="Dispose"/> is called
		/// </summary>
		readonly TaskCompletionSource<object> onDisposal;

		/// <summary>
		/// Construct a <see cref="Repository"/>
		/// </summary>
		/// <param name="_repositoryLib">The value of <see cref="repositoryLib"/></param>
		/// <param name="_onDisposal">The value of <see cref="onDisposal"/></param>
		public Repository(LibGit2Sharp.Repository _repositoryLib, TaskCompletionSource<object> _onDisposal)
		{
			repositoryLib = _repositoryLib ?? throw new ArgumentNullException(nameof(_repositoryLib));
			onDisposal = _onDisposal ?? throw new ArgumentNullException(nameof(_onDisposal));
		}

		/// <summary>
		/// Disposes the <see cref="Repository"/> and completes <see cref="onDisposal"/>
		/// </summary>
		public void Dispose()
		{
			repositoryLib.Dispose();
			onDisposal.SetResult(null);
		}

		/// <inheritdoc />
		public Task Checkout(string commitish, CancellationToken token)
		{
			if (commitish == null)
				throw new ArgumentNullException(nameof(commitish));
			return Task.Factory.StartNew(() => Commands.Checkout(repositoryLib, commitish), token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
		}

		/// <inheritdoc />
		public Task<bool> ContainsCommit(string sha, CancellationToken token)
		{
			return Task.Run(() => repositoryLib.Lookup(sha) != null);
		}

		/// <inheritdoc />
		public Task Fetch(CancellationToken token)
		{
			return Task.Factory.StartNew(() =>
			{
				var remote = repositoryLib.Network.Remotes.First();
				var refSpecs = remote.FetchRefSpecs.Select(X => X.Specification);
				Commands.Fetch(repositoryLib, remote.Name, refSpecs, GenerateFetchOptions(token), "Update of origin branch");
				token.ThrowIfCancellationRequested();
			}, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
		}

		/// <inheritdoc />
		public Task FetchPullRequest(int prNumber, CancellationToken token)
		{
			if (prNumber < 1)
				throw new ArgumentOutOfRangeException(nameof(prNumber), prNumber, String.Format(CultureInfo.CurrentCulture, "{0} must be greater than zero!", nameof(prNumber)));
			return Task.Factory.StartNew(() =>
			{
				var remote = repositoryLib.Network.Remotes.First();
				var prBranchName = String.Format(CultureInfo.InvariantCulture, "pr-{0}", prNumber);
				var refSpecs = new List<string>
				{
					String.Format(CultureInfo.InvariantCulture, "pull/{0}/head:{1}", prNumber, prBranchName)
				};
				Commands.Fetch(repositoryLib, remote.Name, refSpecs, GenerateFetchOptions(token), String.Format(CultureInfo.CurrentCulture, "Fetch of pull request #{0}", prNumber));
				token.ThrowIfCancellationRequested();
			}, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
		}

		/// <inheritdoc />
		public Task Merge(string commitish, CancellationToken token)
		{
			if (commitish == null)
				throw new ArgumentNullException(nameof(commitish));
			return Task.Factory.StartNew(() =>
			{
				if (repositoryLib.Merge(commitish, new Signature(new Identity(nameof(MapDiffBot), String.Format(CultureInfo.InvariantCulture, "{0}@users.noreply.github.com", nameof(MapDiffBot))), DateTime.UtcNow)).Status == MergeStatus.Conflicts)
				{
					repositoryLib.Reset(ResetMode.Hard);
					throw new InvalidOperationException("The resulting merge has conflicts!");
				}
			}, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
		}
	}
}