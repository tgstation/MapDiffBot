using MapDiffBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

		public static string RouteTo(PullRequest pullRequest, int fileId, string postfix) => String.Format(CultureInfo.InvariantCulture, "/Files/{0}/{1}/{2}/{3}.{4}", pullRequest.Base.Repository.Id, pullRequest.Number, fileId, postfix, postfix == "logs" ? "txt" : "png");
		public static string RouteTo(PullRequest pullRequest) => String.Format(CultureInfo.InvariantCulture, "/Files/{0}/{1}/logs.txt", pullRequest.Base.Repository.Id, pullRequest.Number);

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

		[HttpGet("{repositoryId}/{prNumber}/{fileId}/{beforeOrAfter}.png")]
		public async Task<IActionResult> HandleMapGet(long repositoryId, int prNumber, int fileId, string beforeOrAfter, CancellationToken cancellationToken)
		{
			if (beforeOrAfter == null)
				throw new ArgumentNullException(nameof(beforeOrAfter));

			logger.LogTrace("Recieved GET: {0}/{1}/{2}/{3}.png", repositoryId, prNumber, fileId, beforeOrAfter);

			beforeOrAfter = beforeOrAfter.ToUpperInvariant();
			bool before = beforeOrAfter == "BEFORE";
			if (!before && beforeOrAfter != "AFTER")
				return BadRequest();

			var diff = await databaseContext.MapDiffs.Where(x => x.RepositoryId == repositoryId && x.PullRequestNumber == prNumber && x.FileId == fileId).Select(x => before ? x.BeforeImage : x.AfterImage).ToAsyncEnumerable().FirstOrDefault().ConfigureAwait(false);

			if (diff == default(Image))
				return NotFound();

			return new FileContentResult(diff.Data, "image/png");
		}

		[HttpGet("{repositoryId}/{prNumber}/{fileId}/logs.txt")]
		public async Task<IActionResult> HandleLogsGet(long repositoryId, int prNumber, int fileId, CancellationToken cancellationToken)
		{
			logger.LogTrace("Recieved GET: {0}/{1}/{2}.txt", repositoryId, prNumber, fileId);
			var	result = await databaseContext.MapDiffs.Where(x => x.RepositoryId == repositoryId && x.PullRequestNumber == prNumber && x.FileId == fileId).Select(x => x.ErrorMessage).ToAsyncEnumerable().FirstOrDefault().ConfigureAwait(false);
			return result != null ? (IActionResult)Content(result) : NotFound();
		}

		[HttpGet("{repositoryId}/{prNumber}/logs.txt")]
		public async Task<IActionResult> HandleAllLogsGet(long repositoryId, int prNumber, CancellationToken cancellationToken)
		{
			logger.LogTrace("Recieved GET: {0}/{1}/logs.txt", repositoryId, prNumber);
			var results = await databaseContext.MapDiffs.Where(x => x.RepositoryId == repositoryId && x.PullRequestNumber == prNumber).Select(x => x.ErrorMessage).ToAsyncEnumerable().ToList().ConfigureAwait(false);
			return results.Count != 0 ? (IActionResult)Content(String.Join(Environment.NewLine, results)) : NotFound();
		}
	}
}