using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace MapDiffBot.Core
{
	/// <summary>
	/// <see cref="IDashboardAuthorizationFilter"/> for anonymous access
	/// </summary>
	sealed class AnonymousDashboardAuthorizationFilter : IDashboardAuthorizationFilter
	{
		/// <summary>
		/// Check if a given <see cref="DashboardContext"/> is authorized
		/// </summary>
		/// <param name="context">The <see cref="DashboardContext"/> for the operation</param>
		/// <returns><see langword="true"/></returns>
		public bool Authorize([NotNull] DashboardContext context) => true;
	}
}