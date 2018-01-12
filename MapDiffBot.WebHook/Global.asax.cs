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
		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
		protected void Application_Start()
		{
			GlobalConfiguration.Configure(WebHooksConfig.Initialize);
		}
	}
}
