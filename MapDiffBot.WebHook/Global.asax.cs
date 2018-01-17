using System.Diagnostics.CodeAnalysis;
using System.Web;
using System.Web.Http;
using Microsoft.AspNet.WebHooks.Config;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// Handles root level configuration
	/// </summary>
	public class Application : HttpApplication
	{
		/// <summary>
		/// Called on the first request to the <see cref="Application"/>
		/// </summary>
		[SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
		protected void Application_Start()
		{
#if DEBUG
			System.Diagnostics.Debugger.Launch();
#endif
			GlobalConfiguration.Configure((config) =>
			{
				config.MapHttpAttributeRoutes();
				WebHooksConfig.Initialize(config);
				config.InitializeReceiveGitHubWebHooks();
			});
		}
	}
}
