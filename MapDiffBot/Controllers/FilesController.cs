using MapDiffBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MemoryStream = System.IO.MemoryStream;

namespace MapDiffBot.Controllers
{
	/// <summary>
	/// <see cref="Controller"/> used for recieving GitHub webhooks
	/// </summary>
	[Route("Files")]
	public sealed class FilesController : Controller
	{
		/// <summary>
		/// The <see cref="ILogger{TCategoryName}"/> for the <see cref="FilesController"/>
		/// </summary>
		readonly ILogger<FilesController> logger;
		/// <summary>
		/// The <see cref="IDatabaseContext"/> for the <see cref="FilesController"/>
		/// </summary>
		readonly IDatabaseContext databaseContext;

		/// <summary>
		/// Create a route for a <paramref name="pullRequest"/> diff image
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/></param>
		/// <param name="fileId">The <see cref="MapDiff.FileId"/></param>
		/// <param name="postfix">Either "before", "after" or "logs"</param>
		/// <returns>A relative url to the appropriate <see cref="FilesController"/> action</returns>
		public static string RouteTo(PullRequest pullRequest, int fileId, string postfix) => String.Format(CultureInfo.InvariantCulture, "/Files/{0}/{1}/{2}/{3}.{4}", pullRequest.Base.Repository.Id, pullRequest.Number, fileId, postfix, postfix == "logs" ? "txt" : "png");

		/// <summary>
		/// Create a route to the logs for a given <paramref name="pullRequest"/>
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/></param>
		/// <returns>A relative url to the appropriate <see cref="FilesController"/> action</returns>
		public static string RouteToLogs(PullRequest pullRequest) => String.Format(CultureInfo.InvariantCulture, "/Files/{0}/{1}/logs.txt", pullRequest.Base.Repository.Id, pullRequest.Number);

		/// <summary>
		/// Construct a <see cref="FilesController"/>
		/// </summary>
		/// <param name="logger">The value of <see cref="logger"/></param>
		/// <param name="databaseContext">The value of <see cref="databaseContext"/></param>
		public FilesController(ILogger<FilesController> logger, IDatabaseContext databaseContext)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
		}

		/// <summary>
		/// Handle a GET of a <see cref="MapDiff"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="MapDiff.InstallationRepositoryId"/></param>
		/// <param name="prNumber">The <see cref="MapDiff.PullRequestNumber"/></param>
		/// <param name="fileId">The <see cref="MapDiff.FileId"/></param>
		/// <param name="beforeOrAfter">"before" or "after"</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> of the operation</returns>
		[HttpGet("{repositoryId}/{prNumber}/{fileId}/{beforeOrAfter}.png")]
		[ResponseCache(VaryByHeader = "User-Agent", Duration = 60)]
		public async Task<IActionResult> HandleMapGet(long repositoryId, int prNumber, int fileId, string beforeOrAfter, CancellationToken cancellationToken)
		{
			if (beforeOrAfter == null)
				throw new ArgumentNullException(nameof(beforeOrAfter));

			logger.LogTrace("Recieved GET: {0}/{1}/{2}/{3}.png", repositoryId, prNumber, fileId, beforeOrAfter);

			beforeOrAfter = beforeOrAfter.ToUpperInvariant();
			bool before = beforeOrAfter == "BEFORE";
			if (!before && beforeOrAfter != "AFTER")
				return BadRequest();

			var diff = await databaseContext.MapDiffs.Where(x => x.InstallationRepositoryId == repositoryId && x.PullRequestNumber == prNumber && x.FileId == fileId).Select(x => before ? x.BeforeImage : x.AfterImage).ToAsyncEnumerable().FirstOrDefault(cancellationToken).ConfigureAwait(false);

			if (diff == null)
				return NotFound();
			
			return File(diff, "image/png");
		}

		/// <summary>
		/// Get the <see cref="MapDiff.LogMessage"/> of a <see cref="MapDiff"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="MapDiff.InstallationRepositoryId"/></param>
		/// <param name="prNumber">The <see cref="MapDiff.PullRequestNumber"/></param>
		/// <param name="fileId">The <see cref="MapDiff.FileId"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> of the operation</returns>
		[HttpGet("{repositoryId}/{prNumber}/{fileId}/logs.txt")]
		public async Task<IActionResult> HandleLogsGet(long repositoryId, int prNumber, int fileId, CancellationToken cancellationToken)
		{
			logger.LogTrace("Recieved GET: {0}/{1}/{2}.txt", repositoryId, prNumber, fileId);
			var	result = await databaseContext.MapDiffs.Where(x => x.InstallationRepositoryId == repositoryId && x.PullRequestNumber == prNumber && x.FileId == fileId).Select(x => x.LogMessage).ToAsyncEnumerable().FirstOrDefault(cancellationToken).ConfigureAwait(false);
			return result != null ? (IActionResult)Content(result) : NotFound();
		}

		/// <summary>
		/// Get the <see cref="MapDiff.LogMessage"/> of all <see cref="MapDiff"/>s in a <see cref="PullRequest"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="MapDiff.InstallationRepositoryId"/></param>
		/// <param name="prNumber">The <see cref="MapDiff.PullRequestNumber"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> of the operation</returns>
		[HttpGet("{repositoryId}/{prNumber}/logs.txt")]
		public async Task<IActionResult> HandleAllLogsGet(long repositoryId, int prNumber, CancellationToken cancellationToken)
		{
			logger.LogTrace("Recieved GET: {0}/{1}/logs.txt", repositoryId, prNumber);
			var results = await databaseContext.MapDiffs.Where(x => x.InstallationRepositoryId == repositoryId && x.PullRequestNumber == prNumber).Select(x => x.LogMessage).ToAsyncEnumerable().ToList(cancellationToken).ConfigureAwait(false);
			return results.Count != 0 ? (IActionResult)Content(String.Join(Environment.NewLine, results)) : NotFound();
		}
	}
}