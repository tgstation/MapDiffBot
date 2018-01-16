using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <inheritdoc />
	sealed class GitHub : IGitHub
	{
		/// <summary>
		/// The <see cref="GitHubClient"/> used
		/// </summary>
		readonly GitHubClient gitHubClient;

		/// <summary>
		/// The <see cref="User"/> we are using the API with
		/// </summary>
		User knownUser;

		/// <summary>
		/// Construct a <see cref="GitHub"/>
		/// </summary>
		public GitHub()
		{
			gitHubClient = new GitHubClient(new ProductHeaderValue(Definitions.ProductHeader));
		}

		/// <inheritdoc />
		public string AccessToken
		{
			set
			{
				if (gitHubClient.Credentials.GetToken() == value)
					return;
				knownUser = null;
				gitHubClient.Credentials = new Credentials(value);
			}
		}
		
		/// <summary>
		/// Ensures <see cref="AccessToken"/> has been set
		/// </summary>
		void CheckCredentials()
		{
			if (gitHubClient.Credentials.AuthenticationType == AuthenticationType.Anonymous)
				throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "{0} has not been set!", nameof(AccessToken)));
		}

		/// <summary>
		/// Sets <see cref="knownUser"/> if it is <see langword="null"/>
		/// </summary>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		async Task CheckUser()
		{
			if (knownUser != null)
				return;
			knownUser = await gitHubClient.User.Current();
		}

		/// <inheritdoc />
		public async Task<List<string>> GetChangedMapFiles(Octokit.Repository repository, int pullRequestNumber, CancellationToken token)
		{
			if (repository == null)
				throw new ArgumentNullException(nameof(repository));

			if (pullRequestNumber < 1)
				throw new ArgumentOutOfRangeException(nameof(pullRequestNumber), pullRequestNumber, String.Format(CultureInfo.CurrentCulture, "{0} must be greater than zero!", nameof(pullRequestNumber)));

			CheckCredentials();

			var changedFiles = await gitHubClient.PullRequest.Files(repository.Id, pullRequestNumber);

			return changedFiles.Where(x => x.FileName.EndsWith(".dmm")).Select(x => x.FileName).ToList();
		}

		/// <inheritdoc />
		public async Task CreateSingletonComment(Octokit.Repository repository, int issueNumber, string body, CancellationToken token)
		{
			if (repository == null)
				throw new ArgumentNullException(nameof(repository));
			if (body == null)
				throw new ArgumentNullException(nameof(body));

			if (issueNumber < 1)
				throw new ArgumentOutOfRangeException(nameof(issueNumber), issueNumber, String.Format(CultureInfo.CurrentCulture, "{0} must be greater than zero!", nameof(issueNumber)));

			CheckCredentials();

			var openComments = gitHubClient.Issue.Comment.GetAllForIssue(repository.Id, issueNumber);

			await CheckUser();

			foreach (var I in await openComments)
				if (I.User.Id == knownUser.Id)
				{
					await gitHubClient.Issue.Comment.Update(repository.Id, I.Id, body);
					return;
				}

			await gitHubClient.Issue.Comment.Create(repository.Id, issueNumber, body);
		}
	}
}