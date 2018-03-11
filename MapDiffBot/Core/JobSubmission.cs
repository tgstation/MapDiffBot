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
		public string HangfireDisplayName { get; set; }

		/// <summary>
		/// The <see cref="PullRequest.Base"/> <see cref="Repository.Id"/>
		/// </summary>
		public long RepositoryId { get; set; }

		/// <summary>
		/// The <see cref="PullRequest.Number"/>
		/// </summary>
		public int PullRequestNumber { get; set; }

		/// <summary>
		/// The URL to use as a link base
		/// </summary>
		public string BaseUrl { get; set; }

		/// <summary>
		/// Construct a <see cref="JobSubmission"/>
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/> being submitted</param>
		/// <param name="baseUrl">The value of <see cref="BaseUrl"/></param>
		/// <param name="stringLocalizer">The <see cref="IStringLocalizer"/> for formatting <see cref="HangfireDisplayName"/></param>
		public JobSubmission(PullRequest pullRequest, string baseUrl, IStringLocalizer<PayloadProcessor> stringLocalizer)
		{
			if (pullRequest == null)
				throw new ArgumentNullException(nameof(pullRequest));
			if (stringLocalizer == null)
				throw new ArgumentNullException(nameof(stringLocalizer));
			BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
			HangfireDisplayName = stringLocalizer["Generate Map Diffs for #{0} - \"{1}\" ({2})", pullRequest.Number, pullRequest.Title, pullRequest.User.Login, pullRequest.HtmlUrl];
			RepositoryId = pullRequest.Base.Repository.Id;
			PullRequestNumber = pullRequest.Number;
		}

		/// <summary>
		/// Construct a <see cref="JobSubmission"/>
		/// </summary>
		/// <param name="issue">The <see cref="Issue"/> being submitted</param>
		/// <param name="repository">The <see cref="Issue.Repository"/></param>
		/// <param name="baseUrl">The value of <see cref="BaseUrl"/></param>
		/// <param name="stringLocalizer">The <see cref="IStringLocalizer"/> for formatting <see cref="HangfireDisplayName"/></param>
		public JobSubmission(Issue issue, Repository repository, string baseUrl, IStringLocalizer<PayloadProcessor> stringLocalizer)
		{
			if (issue == null)
				throw new ArgumentNullException(nameof(issue));
			if (stringLocalizer == null)
				throw new ArgumentNullException(nameof(stringLocalizer));
			BaseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
			HangfireDisplayName = stringLocalizer["Generate Map Diffs for #{0} - \"{1}\" ({2}) - {3}", issue.Number, issue.Title, issue.User.Login, issue.HtmlUrl];
			RepositoryId = repository.Id;
			PullRequestNumber = issue.Number;
		}

		/// <summary>
		/// Construct a <see cref="JobSubmission"/>. For use by <see cref="Hangfire"/>
		/// </summary>
		[Obsolete("For use by hangfire only", true)]
		public JobSubmission() { }

		/// <inheritdoc />
		public override string ToString() => HangfireDisplayName;
	}
}
