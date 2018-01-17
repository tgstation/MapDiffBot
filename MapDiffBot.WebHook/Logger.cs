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

		/// <summary>
		/// Path to the log file
		/// </summary>
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
		public async Task LogError(string message)
		{
			await ioManager.CreateDirectory(".", CancellationToken.None);
			await ioManager.AppendAllText(logFile, String.Format(CultureInfo.CurrentCulture, "{0}: {1}{2}", DateTime.Now.ToLongTimeString(), message, Environment.NewLine), CancellationToken.None);
		}
	}
}