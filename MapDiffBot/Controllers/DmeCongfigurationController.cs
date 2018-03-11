using System;
using MapDiffBot.Core;
using MapDiffBot.Models;
using Microsoft.AspNetCore.Mvc;

namespace MapDiffBot.Controllers
{
	/// <summary>
	/// Handles configuring <see cref="InstallationRepository.TargetDme"/>s
	/// </summary>
    public sealed class DmeCongfigurationController : Controller
    {
		readonly IDatabaseContext databaseContext;
		readonly IGitHubManager gitHubManager;
		/// <summary>
		/// Construct a <see cref="DmeCongfigurationController"/>
		/// </summary>
		/// <param name="databaseContext">The value of <see cref="databaseContext"/></param>
		/// <param name="gitHubManager">The value of <see cref="gitHubManager"/></param>
		public DmeCongfigurationController(IDatabaseContext databaseContext, IGitHubManager gitHubManager)
		{
			this.databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
			this.gitHubManager = gitHubManager ?? throw new ArgumentNullException(nameof(gitHubManager));
		}
		/*
		/// <summary>
		/// Get the <see cref="InstallationRepository.TargetDme"/> for a given <paramref name="repositoryId"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="InstallationRepository.Id"/></param>
		/// <returns>The <see cref="ActionResult"/> for the operation</returns>
		[HttpGet("{repositoryId}")]
        public ActionResult Index(long repositoryId)
        {
			return View();
        }


		/// <summary>
		/// Get a new <paramref name="dmePath"/> for a given <paramref name="repositoryId"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="InstallationRepository.Id"/></param>
		/// <param name="dmePath">The new <see cref="InstallationRepository.TargetDme"/></param>
		/// <returns>The <see cref="ActionResult"/> for the operation</returns>
		[HttpPost("{repositoryId}/{dmePath}")]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(long repositoryId, string dmePath)
        {
			return View(nameof(Index));
        }
		*/
	}
}