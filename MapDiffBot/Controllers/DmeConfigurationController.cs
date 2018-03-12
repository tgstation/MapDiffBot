using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MapDiffBot.Core;
using MapDiffBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace MapDiffBot.Controllers
{
	/// <summary>
	/// Handles configuring <see cref="InstallationRepository.TargetDme"/>s
	/// </summary>
	[Route(Route)]
    public sealed class DmeConfigurationController : Controller
    {
		/// <summary>
		/// The route to the <see cref="DmeConfigurationController"/>
		/// </summary>
		const string Route = "DmeConfiguration";
		/// <summary>
		/// The <see cref="IDatabaseContext"/> for the <see cref="DmeConfigurationController"/>
		/// </summary>
		readonly IDatabaseContext databaseContext;
		/// <summary>
		/// The <see cref="IGitHubManager"/> for the <see cref="DmeConfigurationController"/>
		/// </summary>
		readonly IGitHubManager gitHubManager;
		/// <summary>
		/// The <see cref="IStringLocalizer"/> for the <see cref="DmeConfigurationController"/>
		/// </summary>
		readonly IStringLocalizer<DmeConfigurationController> stringLocalizer;

		/// <summary>
		/// Construct a <see cref="DmeConfigurationController"/>
		/// </summary>
		/// <param name="databaseContext">The value of <see cref="databaseContext"/></param>
		/// <param name="gitHubManager">The value of <see cref="gitHubManager"/></param>
		/// <param name="stringLocalizer">The value of <see cref="stringLocalizer"/></param>
		public DmeConfigurationController(IDatabaseContext databaseContext, IGitHubManager gitHubManager, IStringLocalizer<DmeConfigurationController> stringLocalizer)
		{
			this.databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
			this.gitHubManager = gitHubManager ?? throw new ArgumentNullException(nameof(gitHubManager));
			this.stringLocalizer = stringLocalizer ?? throw new ArgumentNullException(nameof(stringLocalizer));
		}

		/// <summary>
		/// Handle a authentication completion
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> for the operation</returns>
		[HttpGet("Authorize")]
		public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
		{
			try
			{
				var redirectId = await gitHubManager.CompleteAuthorization(Request, Response.Cookies, cancellationToken).ConfigureAwait(false);
				return RedirectToAction(nameof(Index), new { repositoryId = Convert.ToInt64(redirectId, CultureInfo.InvariantCulture) });
			}
			catch
			{
				return BadRequest();
			}
		}

		/// <summary>
		/// Logs out the user and redirects them to the <see cref="Index(long, CancellationToken)"/> page
		/// </summary>
		/// <param name="repositoryId">The <see cref="InstallationRepository.Id"/></param>
		/// <returns>A <see cref="RedirectToActionResult"/> directed to <see cref="Index(long, CancellationToken)"/></returns>
		[HttpGet("SignOut/{repositoryId}")]
		public RedirectToActionResult SignOut(long repositoryId)
		{
			gitHubManager.ExpireAuthorization(Response.Cookies);
			return RedirectToAction(nameof(Index), new { repositoryId });
		} 

		/// <summary>
		/// Get the <see cref="InstallationRepository.TargetDme"/> for a given <paramref name="repositoryId"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="InstallationRepository.Id"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> for the operation</returns>
		[HttpGet("{repositoryId}")]
        public async Task<IActionResult> Index(long repositoryId, CancellationToken cancellationToken)
        {
			try
			{
				var loadRepoTask = gitHubManager.LoadInstallation(repositoryId, cancellationToken);
				var authedTask = gitHubManager.CheckAuthorization(repositoryId, Request.Cookies, cancellationToken);
				await loadRepoTask.ConfigureAwait(false);

				ViewBag.ConfiguredDme = await databaseContext.InstallationRepositories.Where(x => x.Id == repositoryId).Select(x => x.TargetDme).ToAsyncEnumerable().First(cancellationToken).ConfigureAwait(false);
				var authLevel = await authedTask.ConfigureAwait(false);
		
				ViewBag.SignIn = stringLocalizer[authLevel == AuthenticationLevel.LoggedOut ? "Sign-In with GitHub" : "Log Out"];
				if (authLevel == AuthenticationLevel.LoggedOut)
					ViewBag.SignInHref = gitHubManager.GetAuthorizationURL(new Uri(String.Concat("https://", Request.Host, Request.PathBase, '/', Route, '/', nameof(Authorize))), repositoryId.ToString(CultureInfo.InvariantCulture));
				else
					ViewBag.SignInHref = Url.Action(nameof(SignOut), new { repositoryId });

				ViewBag.SignOutHref = stringLocalizer["Sign-In with GitHub"];
				ViewBag.Title = stringLocalizer["DME Configuration"];
				ViewBag.HideLogin = false;
				ViewBag.EditHeader = stringLocalizer["Set the path to the .dme MapDiffBot should use for rendering"];
				ViewBag.AutoDme = stringLocalizer["Auto-Detect"];
				ViewBag.Submit = stringLocalizer["Submit"];
				ViewBag.OnlyMemes = stringLocalizer["Only maintainers can perform this action!"];

				return View();
			}
			catch
			{
				return NotFound();
			}
        }

		/// <summary>
		/// Get a new <see cref="InstallationRepository.TargetDme"/> for a given <see cref="InstallationRepository.Id"/>
		/// </summary>
		/// <param name="repositoryId">The <see cref="InstallationRepository.Id"/></param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> for the operation</returns>
		[HttpPost("{repositoryId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long repositoryId, CancellationToken cancellationToken)
        {
			try
			{
				string newDmePath = Request.Form[nameof(newDmePath)];
				var authed = await gitHubManager.CheckAuthorization(repositoryId, Request.Cookies, cancellationToken).ConfigureAwait(false);
				switch (authed)
				{
					case AuthenticationLevel.LoggedOut:
						return Unauthorized();
					case AuthenticationLevel.User:
						return Forbid();
				}

				databaseContext.InstallationRepositories.Attach(new InstallationRepository
				{
					Id = repositoryId,
					TargetDme = newDmePath
				}).Property(nameof(InstallationRepository.TargetDme)).IsModified = true;
				await databaseContext.Save(cancellationToken).ConfigureAwait(false);
				return RedirectToAction(nameof(Index), new { repositoryId });
			}
			catch
			{
				return NotFound();
			}
        }
	}
}