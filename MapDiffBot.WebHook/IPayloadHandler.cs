using Microsoft.AspNet.WebHooks;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// Handles a GitHub payload. As they are constructed via reflection they must have a public constructor that takes an <see cref="IIOManager"/> and a <see cref="ILogger"/> in that order
	/// </summary>
	interface IPayloadHandler
	{
		/// <summary>
		/// The name of the payload event this <see cref="IPayloadHandler"/> handles
		/// </summary>
		string EventType { get; }

		/// <summary>
		/// Handle a <paramref name="payload"/>
		/// </summary>
		/// <param name="payload">A <see cref="JObject"/> representation of the payload</param>
		/// <param name="config">The <see cref="IWebHookReceiverConfig"/> for the operation</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Run(JObject payload, IWebHookReceiverConfig config, CancellationToken token);
	}
}
