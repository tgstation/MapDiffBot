using Microsoft.AspNet.WebHooks;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// Handles recieving GitHub webhooks
	/// </summary>
	[SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Git")]
	public sealed class GitHubWebHookHandler : WebHookHandler
	{
		/// <summary>
		/// The <see cref="IPayloadDelegator"/> for the <see cref="GitHubWebHookHandler"/>
		/// </summary>
		readonly IPayloadDelegator payloadDelegator;

		/// <summary>
		/// Construct a <see cref="GitHubWebHookHandler"/>
		/// </summary>
		public GitHubWebHookHandler() : this(new ResolvingIOManager(new DefaultIOManager(), AppDomain.CurrentDomain.GetData("DataDirectory").ToString())) { }

		/// <summary>
		/// Construct a <see cref="GitHubWebHookHandler"/>
		/// </summary>
		/// <param name="_ioManager">The <see cref="IIOManager"/> used to construct the <see cref="payloadDelegator"/> and <see cref="Logger"/></param>
		GitHubWebHookHandler(IIOManager _ioManager) : this(_ioManager, new Logger(_ioManager)) { }

		/// <summary>
		/// Construct a <see cref="GitHubWebHookHandler"/>
		/// </summary>
		/// <param name="_ioManager">The <see cref="IIOManager"/> used to construct the <see cref="payloadDelegator"/></param>
		/// <param name="_logger">The <see cref="ILogger"/> used to construct the <see cref="payloadDelegator"/></param>
		GitHubWebHookHandler(IIOManager _ioManager, ILogger _logger) : this(new PayloadDelegator(_ioManager, _logger)) { }

		/// <summary>
		/// Construct a <see cref="GitHubWebHookHandler"/>
		/// </summary>
		/// <param name="_payloadDelegator">The value of <see cref="payloadDelegator"/></param>
		GitHubWebHookHandler(IPayloadDelegator _payloadDelegator)
		{
			payloadDelegator = _payloadDelegator ?? throw new ArgumentNullException(nameof(_payloadDelegator));
		}

		/// <summary>
		/// Called when a webhook is recieved
		/// </summary>
		/// <param name="receiver">The name of the webhook generator</param>
		/// <param name="context">The <see cref="WebHookHandlerContext"/></param>
		/// <returns>A <see cref="Task"/> representing the operation</returns>
		public override async Task ExecuteAsync(string receiver, WebHookHandlerContext context)
		{
			try
			{
				var config = context.Request.GetConfiguration().DependencyResolver.GetReceiverConfig();
				await payloadDelegator.ProcessPayload(context.Actions.FirstOrDefault(), context.GetDataOrDefault<JObject>(), config);
			}
			catch (ArgumentNullException e)
			{
				throw new HttpException(401, "Missing action or payload!", e);
			}
			catch (NotImplementedException e)
			{
				throw new HttpException(404, "No handler for event!", e);
			}
		}
	}
}