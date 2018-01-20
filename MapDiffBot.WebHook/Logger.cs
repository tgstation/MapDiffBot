using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <inheritdoc />
	sealed class Logger : ILogger
	{
		public const string OutputFileExceptionKey = "LoggerOutputFile";

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

		/// <summary>
		/// Writes <paramref name="errorLogMessage"/> to <see cref="logFile"/>
		/// </summary>
		/// <param name="errorLogMessage">The message to log</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		async Task WriteToMainLog(string errorLogMessage, CancellationToken token)
		{
			await ioManager.CreateDirectory(".", token);
			await ioManager.AppendAllText(logFile, errorLogMessage, token);
		}

		/// <inheritdoc />
		public async Task LogException(Exception exception)
		{
			var message = exception.ToString();
			var errorLogMessage = String.Format(CultureInfo.CurrentCulture, "{0}: {1}{2}", DateTime.Now.ToLongTimeString(), message, Environment.NewLine);
			var mainLogTask = WriteToMainLog(errorLogMessage, CancellationToken.None);
			if (exception.Data.Contains(OutputFileExceptionKey))
				await ioManager.AppendAllText(exception.Data[OutputFileExceptionKey].ToString(), errorLogMessage, CancellationToken.None);
			await mainLogTask;
		}
	}
}