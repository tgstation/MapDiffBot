using Microsoft.Extensions.Localization;
using Octokit;
using System;

namespace MapDiffBot.Core
{
	/// <summary>
	/// A job for <see cref="PayloadProcessor.ScanPullRequest(JobSubmission, Hangfire.IJobCancellationToken)"/>
	/// </summary>
    sealed class JobSubmission
    {
		/// <summary>
		/// The text for the <see cref="Hangfire.Dashboard"/>
		/// </summary>
		readonly string hangfireDisplayName;

		/// <summary>
		/// The <see cref="PullRequest.Base"/> <see cref="Repository.Id"/>
		/// </summary>
		public long RepositoryId { get; }

		/// <summary>
		/// The <see cref="PullRequest.Number"/>
		/// </summary>
		public int PullRequestNumber { get; }

		/// <summary>
		/// The URL to use as a link base
		/// </summary>
		public string BaseUrl { get; }

		/// <summary>
		/// Construct a <see cref="JobSubmission"/>
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/> being submitted</param>
		/// <param name="baseUrl">The value of <see cref="BaseUrl"/></param>
		/// <param name="stringLocalizer">The <see cref="IStringLocalizer"/> for formatting <see cref="hangfireDisplayName"/></param>
		public JobSubmission(PullRequest pullRequest, string baseUrl, IStringLocalizer<PayloadProcessor> stringLocalizer)
		{
			if (pullRequest == null)
				throw new ArgumentNullException(nameof(pullRequest));
			if (stringLocalizer == null)
				throw new ArgumentNullException(nameof(stringLocalizer));
			BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
			hangfireDisplayName = stringLocalizer["Generate Map Diffs for #{0} - \"{1}\" ({2})", pullRequest.Number, pullRequest.Title, pullRequest.User.Login, pullRequest.HtmlUrl];
			RepositoryId = pullRequest.Base.Repository.Id;
			PullRequestNumber = pullRequest.Number;
		}

		/// <summary>
		/// Construct a <see cref="JobSubmission"/>
		/// </summary>
		/// <param name="issue">The <see cref="Issue"/> being submitted</param>
		/// <param name="baseUrl">The value of <see cref="BaseUrl"/></param>
		/// <param name="stringLocalizer">The <see cref="IStringLocalizer"/> for formatting <see cref="hangfireDisplayName"/></param>
		public JobSubmission(Issue issue, string baseUrl, IStringLocalizer<PayloadProcessor> stringLocalizer)
		{
			if (issue == null)
				throw new ArgumentNullException(nameof(issue));
			if (stringLocalizer == null)
				throw new ArgumentNullException(nameof(stringLocalizer));
			BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
			hangfireDisplayName = stringLocalizer["Generate Map Diffs for #{0} - \"{1}\" ({2})", issue.Number, issue.Title, issue.User.Login, issue.HtmlUrl];
			RepositoryId = issue.Repository.Id;
			PullRequestNumber = issue.Number;
		}

		/// <inheritdoc />
		public override string ToString() => hangfireDisplayName;
	}
}
