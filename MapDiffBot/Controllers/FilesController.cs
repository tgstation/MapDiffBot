using MapDiffBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
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
	[Route(Route)]
	public sealed class FilesController : Controller
	{
		/// <summary>
		/// The route to the <see cref="FilesController"/>
		/// </summary>
		const string Route = "Files";

		/// <summary>
		/// The <see cref="ILogger{TCategoryName}"/> for the <see cref="FilesController"/>
		/// </summary>
		readonly ILogger<FilesController> logger;
		/// <summary>
		/// The <see cref="IDatabaseContext"/> for the <see cref="FilesController"/>
		/// </summary>
		readonly IDatabaseContext databaseContext;
		/// <summary>
		/// The <see cref="IStringLocalizer"/> for the <see cref="FilesController"/>
		/// </summary>
		readonly IStringLocalizer<FilesController> stringLocalizer;

		/// <summary>
		/// Create a route for a <paramref name="pullRequest"/> diff image
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/></param>
		/// <param name="fileId">The <see cref="MapDiff.FileId"/></param>
		/// <param name="postfix">Either "before", "after" or "logs"</param>
		/// <returns>A relative url to the appropriate <see cref="FilesController"/> action</returns>
		public static string RouteTo(PullRequest pullRequest, int fileId, string postfix) => String.Format(CultureInfo.InvariantCulture, "/{5}/{0}/{1}/{2}/{3}.{4}", pullRequest.Base.Repository.Id, pullRequest.Number, fileId, postfix, postfix == "logs" ? "txt" : "png", Route);

		/// <summary>
		/// Create a route to the <see cref="Browse(long, int, CancellationToken)"/> page for a given <paramref name="pullRequest"/>
		/// </summary>
		/// <param name="pullRequest">The <see cref="PullRequest"/></param>
		/// <returns>A relative url to the appropriate <see cref="FilesController"/> action</returns>
		public static string RouteToBrowse(PullRequest pullRequest) => String.Format(CultureInfo.InvariantCulture, "/{2}/{0}/{1}", pullRequest.Base.Repository.Id, pullRequest.Number, Route);

		/// <summary>
		/// Construct a <see cref="FilesController"/>
		/// </summary>
		/// <param name="logger">The value of <see cref="logger"/></param>
		/// <param name="databaseContext">The value of <see cref="databaseContext"/></param>
		/// <param name="stringLocalizer">The value of <see cref="stringLocalizer"/></param>
		public FilesController(ILogger<FilesController> logger, IDatabaseContext databaseContext, IStringLocalizer<FilesController> stringLocalizer)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
			this.stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
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

		/// <summary>
		/// Gets the browse page for a set of <see cref="MapDiff"/>s
		/// </summary>
		/// <param name="repositoryId">The <see cref="MapDiff.InstallationRepositoryId"/></param>
		/// <param name="prNumber">The <see cref="MapDiff.PullRequestNumber"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> of the operation</returns>
		[HttpGet("{repositoryId}/{prNumber}")]
		public async Task<IActionResult> Browse(long repositoryId, int prNumber, CancellationToken cancellationToken)
		{
			var diffs = await databaseContext.MapDiffs.Where(x => x.InstallationRepositoryId == repositoryId && x.PullRequestNumber == prNumber).Select(x => x.MapPath).ToAsyncEnumerable().ToList(cancellationToken).ConfigureAwait(false);

			if (diffs.Count == 0)
				return NotFound();

			ViewBag.Title = stringLocalizer["Pull Request #{0}", prNumber];
			ViewBag.HideLogin = true;
			ViewBag.Diffs = diffs;
			ViewBag.RepositoryId = repositoryId;
			ViewBag.PRNumber = prNumber;
			ViewBag.Logs = stringLocalizer["Logs"];
			ViewBag.AllLogs = stringLocalizer["All Logs"];
			ViewBag.Configure = stringLocalizer["Configure"];
			ViewBag.Before = stringLocalizer["Before"];
			ViewBag.After = stringLocalizer["After"];
			ViewBag.MapDiffs = stringLocalizer["Map diffs"];
			return View();
		}
	}
}