using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <inheritdoc />
	sealed class Logger : ILogger
	{
		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="Logger"/>
		/// </summary>
		readonly IIOManager ioManager;

		readonly string logFile;

		/// <summary>
		/// Construct a <see cref="Logger"/>
		/// </summary>
		/// <param name="_ioManager">The value of <see cref="ioManager"/></param>
		public Logger(IIOManager _ioManager)
		{
			ioManager = new ResolvingIOManager(_ioManager ?? throw new ArgumentNullException(nameof(_ioManager)), "Logs");
			logFile = String.Format(CultureInfo.InvariantCulture, "{0}.txt", DateTime.Now.Ticks);
		}

		/// <inheritdoc />
		public Task LogError(string message)
		{
			return ioManager.AppendAllText(logFile, String.Format(CultureInfo.CurrentCulture, "{0}: {1}", DateTime.Now.ToLongTimeString(), message), CancellationToken.None);
		}
	}
}