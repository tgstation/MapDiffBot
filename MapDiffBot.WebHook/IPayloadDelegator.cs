using Microsoft.AspNet.WebHooks;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// Sends GitHub payloads to the correct <see cref="IPayloadHandler"/>
	/// </summary>
	interface IPayloadDelegator
	{
		/// <summary>
		/// Process a <paramref name="payload"/> asyncronously
		/// </summary>
		/// <param name="action">The action header of the <paramref name="payload"/></param>
		/// <param name="payload">The JSON payload formatted as a <see cref="JObject"/></param>
		/// <param name="config">The configuration for the payload request</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task ProcessPayload(string action, JObject payload, IWebHookReceiverConfig config);
	}
}
