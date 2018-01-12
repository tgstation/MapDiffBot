using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNet.WebHooks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// Handles recieving GitHub webhooks
	/// </summary>
	[SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Git")]
	public sealed class GitHubWebHookHandler : WebHookHandler
	{
		/// <summary>
		/// Construct a <see cref="GitHubWebHookHandler"/>
		/// </summary>
		public GitHubWebHookHandler()
		{
			Receiver = GitHubWebHookReceiver.ReceiverName;
		}

		/// <summary>
		/// Called when a webhook is recieved
		/// </summary>
		/// <param name="receiver">The name of the webhook generator</param>
		/// <param name="context">The <see cref="WebHookHandlerContext"/></param>
		/// <returns>A <see cref="Task"/> representing the operation</returns>
		public override Task ExecuteAsync(string receiver, WebHookHandlerContext context)
		{
			throw new NotImplementedException();
		}
	}
}