using GitHubJwt;
using MapDiffBot.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
#pragma warning disable CA1812
	sealed class GitHubClientFactory : IGitHubClientFactory, IPrivateKeySource
#pragma warning restore CA1812
	{
		//TODO: make this private
		/// <summary>
		/// The user agent string to provide to various APIs
		/// </summary>
		public static readonly string userAgent = String.Format(CultureInfo.InvariantCulture, "MapDiffBot-v{0}", Assembly.GetExecutingAssembly().GetName().Version);

		/// <summary>
		/// Creates a <see cref="GitHubClient"/> with the correct <see cref="ProductHeaderValue"/>
		/// </summary>
		/// <returns>A new <see cref="GitHubClient"/></returns>
		static GitHubClient CreateBareClient() => new GitHubClient(new ProductHeaderValue(userAgent));

		/// <summary>
		/// The <see cref="GitHubConfiguration"/> for the <see cref="GitHubClientFactory"/>
		/// </summary>
		readonly GitHubConfiguration gitHubConfiguration;
		/// <summary>
		/// The <see cref="IWebRequestManager"/> for the <see cref="GitHubClientFactory"/>
		/// </summary>
		readonly IWebRequestManager webRequestManager;

		/// <summary>
		/// Construct a <see cref="GitHubClientFactory"/>
		/// </summary>
		/// <param name="gitHubConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing the value of <see cref="gitHubConfiguration"/></param>
		/// <param name="webRequestManager">The value of <see cref="webRequestManager"/></param>
		public GitHubClientFactory(IOptions<GitHubConfiguration> gitHubConfigurationOptions, IWebRequestManager webRequestManager)
		{
			gitHubConfiguration = gitHubConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(gitHubConfigurationOptions));
			this.webRequestManager = webRequestManager ?? throw new ArgumentNullException(nameof(webRequestManager));
		}

		/// <inheritdoc />
		public IGitHubClient CreateAppClient()
		{
			//use app auth, max expiration time
			var jwtFac = new GitHubJwtFactory(this, new GitHubJwtFactoryOptions { AppIntegrationId = gitHubConfiguration.AppID, ExpirationSeconds = 600 });
			var jwt = jwtFac.CreateEncodedJwtToken();
			var client = CreateBareClient();
			client.Credentials = new Credentials(jwt, AuthenticationType.Bearer);
			return client;
		}

		/// <inheritdoc />
		public IGitHubClient CreateOauthClient(string accessToken)
		{
			var client = CreateBareClient();
			client.Credentials = new Credentials(accessToken ?? throw new ArgumentNullException(nameof(accessToken)), AuthenticationType.Oauth);
			return client;
		}

		/// <inheritdoc />
		public TextReader GetPrivateKeyReader() => new StringReader(gitHubConfiguration.PemData);

		/// <inheritdoc />
		public async Task<IReadOnlyList<Repository>> GetInstallationRepositories(string installationToken, CancellationToken cancellationToken)
		{
			var json = await webRequestManager.RunGet(new Uri("https://api.github.com/installation/repositories"), new List<string> { "Accept: application/vnd.github.machine-man-preview+json", string.Format(CultureInfo.InvariantCulture, "User-Agent: {0}", userAgent) , String.Format(CultureInfo.InvariantCulture, "Authorization: bearer {0}", installationToken) }, cancellationToken).ConfigureAwait(false);
			var jsonObj = JObject.Parse(json);
			var array = jsonObj["repositories"];
			return new SimpleJsonSerializer().Deserialize<List<Repository>>(array.ToString());
		}
	}
}