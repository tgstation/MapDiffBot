using MapDiffBot.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace MapDiffBot.Controllers
{
	/// <summary>
	/// <see cref="Controller"/> used for manually <see cref="IPayloadProcessor"/> jobs
	/// </summary>
	[Route("Trigger")]
	public sealed class TriggerController : Controller
	{
		/// <summary>
		/// The <see cref="ILogger{TCategoryName}"/> for the <see cref="PayloadsController"/>
		/// </summary>
		readonly ILogger<TriggerController> logger;
		/// <summary>
		/// The <see cref="IPayloadProcessor"/> for the <see cref="PayloadsController"/>
		/// </summary>
		readonly IPayloadProcessor payloadProcessor;
		/// <summary>
		/// The <see cref="IGitHubManager"/> for the <see cref="PayloadsController"/>
		/// </summary>
		readonly IGitHubManager gitHubManager;

		/// <summary>
		/// Construct a <see cref="TriggerController"/>
		/// </summary>
		/// <param name="logger">The value of <see cref="logger"/></param>
		/// <param name="payloadProcessor">The value of <see cref="payloadProcessor"/></param>
		/// <param name="gitHubManager">The value of <see cref="gitHubManager"/></param>
		public TriggerController(ILogger<TriggerController> logger, IPayloadProcessor payloadProcessor, IGitHubManager gitHubManager)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.payloadProcessor = payloadProcessor ?? throw new ArgumentNullException(nameof(payloadProcessor));
			this.gitHubManager = gitHubManager ?? throw new ArgumentNullException(nameof(gitHubManager));
		}
		
		/// <summary>
		/// Handle a GET to the <see cref="TriggerController"/>
		/// </summary>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> of the POST</returns>
		[HttpGet("{reposiotoryId}/{prNumber}")]
		public async Task<IActionResult> Receive(long repositoryId, int prNumber, CancellationToken cancellationToken)
		{
			logger.LogInformation("Triggering job for {0}/{1}", repositoryId, prNumber);
			var pr = await gitHubManager.GetPullRequest(repositoryId, prNumber, cancellationToken).ConfigureAwait(false);
			payloadProcessor.ProcessPullRequest(pr);
			return Redirect(pr.HtmlUrl);
		}
	}
}