using MapDiffBot.Configuration;
using MapDiffBot.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Octokit;
using Octokit.Internal;
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MapDiffBot.Controllers
{
	/// <summary>
	/// <see cref="Controller"/> used for recieving GitHub webhooks
	/// </summary>
	[Produces("application/json")]
	[Route("Payloads")]
	public sealed class PayloadsController : Controller
	{
		/// <summary>
		/// The <see cref="GitHubConfiguration"/> for the <see cref="PayloadsController"/>
		/// </summary>
		readonly GitHubConfiguration gitHubConfiguration;
		/// <summary>
		/// The <see cref="ILogger{TCategoryName}"/> for the <see cref="PayloadsController"/>
		/// </summary>
		readonly ILogger<PayloadsController> logger;
		/// <summary>
		/// The <see cref="IPayloadProcessor"/> for the <see cref="PayloadsController"/>
		/// </summary>
		readonly IPayloadProcessor pullRequestProcessor;
		/// <summary>
		/// The <see cref="IGitHubManager"/> for the <see cref="PayloadsController"/>
		/// </summary>
		readonly IGitHubManager gitHubManager;

		/// <summary>
		/// Convert some <paramref name="bytes"/> to a hex string
		/// </summary>
		/// <param name="bytes">The <see cref="byte"/> array to convert</param>
		/// <returns><paramref name="bytes"/> as a hex string</returns>
		static string ToHexString(byte[] bytes)
		{
			var builder = new StringBuilder(bytes.Length * 2);
			foreach (byte b in bytes)
				builder.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", b);
			return builder.ToString();
		}

		/// <summary>
		/// Construct a <see cref="PayloadsController"/>
		/// </summary>
		/// <param name="gitHubConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing value of <see cref="gitHubConfiguration"/></param>
		/// <param name="logger">The value of <see cref="logger"/></param>
		/// <param name="pullRequestProcessor">The value of <see cref="pullRequestProcessor"/></param>
		/// <param name="gitHubManager">The value of <see cref="gitHubManager"/></param>
		public PayloadsController(IOptions<GitHubConfiguration> gitHubConfigurationOptions, ILogger<PayloadsController> logger, IPayloadProcessor pullRequestProcessor, IGitHubManager gitHubManager)
		{
			gitHubConfiguration = gitHubConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(gitHubConfigurationOptions));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.pullRequestProcessor = pullRequestProcessor ?? throw new ArgumentNullException(nameof(pullRequestProcessor));
			this.gitHubManager = gitHubManager ?? throw new ArgumentNullException(nameof(gitHubManager));
		}

		/// <summary>
		/// Check that a <paramref name="payload"/> matches it's <paramref name="signatureWithPrefix"/> for the configured secret
		/// </summary>
		/// <param name="payload">The json payload</param>
		/// <param name="signatureWithPrefix">The SHA1 signature</param>
		/// <returns><see langword="true"/> if the <paramref name="payload"/> matches it's <paramref name="signatureWithPrefix"/> for the configured secret</returns>
		bool CheckPayloadSignature(string payload, string signatureWithPrefix)
		{
			const string Sha1Prefix = "sha1=";
			if (!signatureWithPrefix.StartsWith(Sha1Prefix, StringComparison.OrdinalIgnoreCase))
				return false;
			var signature = signatureWithPrefix.Substring(Sha1Prefix.Length);
			var secret = Encoding.UTF8.GetBytes(gitHubConfiguration.WebhookSecret);
			var payloadBytes = Encoding.UTF8.GetBytes(payload);

			byte[] hash;
#pragma warning disable CA5350 // Do not use insecure cryptographic algorithm SHA1.
			using (var hmSha1 = new HMACSHA1(secret))
#pragma warning restore CA5350 // Do not use insecure cryptographic algorithm SHA1.
				hash = hmSha1.ComputeHash(payloadBytes);
			var expectedHash = ToHexString(hash);
			logger.LogTrace("Expect: {0}. Received: {1}", expectedHash, signature);
			return expectedHash == signature;
		}

		/// <summary>
		/// Handle a POST to the <see cref="PayloadsController"/>
		/// </summary>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> of the POST</returns>
		[HttpPost]
		public async Task<IActionResult> Receive()
		{
			logger.LogTrace("Recieved POST.");

			if (!Request.Headers.TryGetValue("X-GitHub-Event", out StringValues eventName)
				|| !Request.Headers.TryGetValue("X-Hub-Signature", out StringValues signature)
				|| !Request.Headers.TryGetValue("X-GitHub-Delivery", out StringValues delivery))
			{
				logger.LogDebug("Missing GitHub headers for payload! Found headers: {0}", Request.Headers.Keys);
				return BadRequest();
			}

			string json;
			using (var reader = new StreamReader(Request.Body))
				json = await reader.ReadToEndAsync().ConfigureAwait(false);

			logger.LogTrace("Recieved payload: {0}", json);

			if (!CheckPayloadSignature(json, signature))
			{
				logger.LogDebug("Payload rejected due to bad signature!");
				return Unauthorized();
			}

			if (eventName == "check_suite")
			{
				CheckSuiteEventPayload payload;
				logger.LogTrace("Deserializing check suite payload.");
				try
				{
					payload = new SimpleJsonSerializer().Deserialize<CheckSuiteEventPayload>(json);
				}
				catch (Exception e)
				{
					logger.LogDebug(e, "Failed to deserialize check suite payload JSON!");
					return BadRequest(e);
				}
				logger.LogTrace("Queuing pull request payload processing job.");

				bool rerequest = payload.Action == "rerequested";
				if (rerequest || payload.Action == "requested")
					pullRequestProcessor.ProcessCheckSuite(payload.CheckSuite, rerequest);
			}

			return Ok();
		}
	}
}