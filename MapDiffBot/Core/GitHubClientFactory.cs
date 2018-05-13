using GitHubJwt;
using MapDiffBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
#pragma warning disable CA1812
	sealed class GitHubClientFactory : IGitHubClientFactory, IPrivateKeySource, IHttpClient
#pragma warning restore CA1812
	{
		/// <summary>
		/// The user agent string to provide to various APIs
		/// </summary>
		static readonly string userAgent = String.Format(CultureInfo.InvariantCulture, "MapDiffBot-v{0}", Assembly.GetExecutingAssembly().GetName().Version);

		/// <summary>
		/// The <see cref="GitHubConfiguration"/> for the <see cref="GitHubClientFactory"/>
		/// </summary>
		readonly GitHubConfiguration gitHubConfiguration;
		/// <summary>
		/// The <see cref="IWebRequestManager"/> for the <see cref="GitHubClientFactory"/>
		/// </summary>
		readonly IWebRequestManager webRequestManager;
		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="GitHubClientFactory"/>
		/// </summary>
		readonly ILogger logger;
		/// <summary>
		/// The <see cref="HttpClientAdapter"/> for the <see cref="GitHubClientFactory"/>
		/// </summary>
		readonly HttpClientAdapter httpClientAdapter;

		/// <summary>
		/// Construct a <see cref="GitHubClientFactory"/>
		/// </summary>
		/// <param name="gitHubConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing the value of <see cref="gitHubConfiguration"/></param>
		/// <param name="webRequestManager">The value of <see cref="webRequestManager"/></param>
		/// <param name="logger">The value of <see cref="logger"/></param>
		public GitHubClientFactory(IOptions<GitHubConfiguration> gitHubConfigurationOptions, IWebRequestManager webRequestManager, ILogger<GitHubClientFactory> logger)
		{
			gitHubConfiguration = gitHubConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(gitHubConfigurationOptions));
			this.webRequestManager = webRequestManager ?? throw new ArgumentNullException(nameof(webRequestManager));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// Creates a <see cref="GitHubClient"/> with the correct <see cref="ProductHeaderValue"/>
		/// </summary>
		/// <returns>A new <see cref="GitHubClient"/></returns>
		GitHubClient CreateBareClient() => new GitHubClient(new Connection(new ProductHeaderValue(userAgent), this));

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
			if (accessToken == null)
				throw new ArgumentNullException(nameof(accessToken));
			var client = CreateBareClient();
			client.Credentials = new Credentials(accessToken, AuthenticationType.Oauth);
			return client;
		}

		/// <inheritdoc />
		public TextReader GetPrivateKeyReader()
		{
			logger.LogTrace("Opening private key file: {0}", gitHubConfiguration.PemPath);
			return File.OpenText(gitHubConfiguration.PemPath);
		}
		
		/// <inheritdoc />
		public async Task<IReadOnlyList<Repository>> GetInstallationRepositories(string installationToken, CancellationToken cancellationToken)
		{
			var json = await webRequestManager.RunGet(new Uri("https://api.github.com/installation/repositories"), new List<string> { "Accept: application/vnd.github.machine-man-preview+json", String.Format(CultureInfo.InvariantCulture, "User-Agent: {0}", userAgent) , String.Format(CultureInfo.InvariantCulture, "Authorization: bearer {0}", installationToken) }, cancellationToken).ConfigureAwait(false);
			var jsonObj = JObject.Parse(json);
			var array = jsonObj["repositories"];
			return new SimpleJsonSerializer().Deserialize<List<Repository>>(array.ToString());
		}

		/// <inheritdoc />
		public Task<IResponse> Send(IRequest request, CancellationToken cancellationToken)
		{
			if(request.Method.Method == HttpMethod.Post.Method)
				logger.LogTrace("Octokit POST:\n{0}", request.Body);
			return httpClientAdapter.Send(request, cancellationToken);
		}

		/// <inheritdoc />
		public void SetRequestTimeout(TimeSpan timeout) => httpClientAdapter.SetRequestTimeout(timeout);

		/// <inheritdoc />
		public void Dispose() => httpClientAdapter.Dispose();
	}
}
