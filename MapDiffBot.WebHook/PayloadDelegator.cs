using Microsoft.AspNet.WebHooks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Hosting;

namespace MapDiffBot.WebHook
{
	/// <inheritdoc />
	sealed class PayloadDelegator : IPayloadDelegator
	{
		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="PayloadDelegator"/>
		/// </summary>
		readonly IIOManager ioManager;
		/// <summary>
		/// The <see cref="ILogger"/> for the <see cref="PayloadDelegator"/>
		/// </summary>
		readonly ILogger logger;

		/// <summary>
		/// Map of actions to their <see cref="IPayloadHandler"/>s
		/// </summary>
		Dictionary<string, List<IPayloadHandler>> payloadMappings;

		/// <summary>
		/// Construct a <see cref="PayloadDelegator"/>
		/// <paramref name="_ioManager">The value of <see cref="ioManager"/></paramref>
		/// <paramref name="_logger">The value of <see cref="logger"/></paramref>
		/// </summary>
		public PayloadDelegator(IIOManager _ioManager, ILogger _logger)
		{
			logger = _logger ?? throw new ArgumentNullException(nameof(_logger));
			ioManager = new ResolvingIOManager(_ioManager ?? throw new ArgumentNullException(nameof(_ioManager)), "Requests");
		}

		/// <summary>
		/// Load the mappings of <see cref="IPayloadHandler.EventType"/> to <see cref="IPayloadHandler"/> using reflection from all <see cref="System.Reflection.Assembly"/>s in the <see cref="AppDomain"/> into <see cref="payloadMappings"/>
		/// </summary>s>
		async Task LoadPayloadMappings()
		{
			Task task = null;
			lock (this)
			{
				if (payloadMappings != null)
					return;

				var types = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(s => s.GetTypes())
					.Where(p => typeof(IPayloadHandler).IsAssignableFrom(p) && !p.IsInterface);
				payloadMappings = new Dictionary<string, List<IPayloadHandler>>();

				foreach (var I in types)
				{
					IPayloadHandler handler;
					try
					{
						handler = (IPayloadHandler)Activator.CreateInstance(I, ioManager, logger);
					}
					catch (MissingMethodException e)
					{
						Task loggerInvocation()
						{
							return logger.LogException(e);
						}
						if (task != null)
							task = task.ContinueWith(async (t) =>
							{
								await loggerInvocation();
							});
						else
							task = loggerInvocation();
						continue;
					}
					if (!payloadMappings.ContainsKey(handler.EventType))
						payloadMappings.Add(handler.EventType, new List<IPayloadHandler> { handler });
					else
						payloadMappings[handler.EventType].Add(handler);
				}
			}
			if (task != null)
				await task;
		}

		/// <inheritdoc />
		public async Task ProcessPayload(string action, JObject json, IWebHookReceiverConfig config)
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));
			if (json == null)
				throw new ArgumentNullException(nameof(json));

			await LoadPayloadMappings();

			if (!payloadMappings.TryGetValue(action, out List<IPayloadHandler> handlers))
				throw new NotImplementedException(String.Format(CultureInfo.CurrentCulture, "No handler for action {0}!", action));

			HostingEnvironment.QueueBackgroundWorkItem(async token =>
			{
				try
				{
					foreach (var I in handlers)
						try
						{
							await I.Run(json, config, token);
						}
						catch (NotImplementedException) { }
				}
				catch (OperationCanceledException) { }
				catch (Exception e)
				{
					await logger.LogException(e);
				}
			});
		}
	}
}