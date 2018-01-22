using System;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see langword="interface"/> for handling various log messages
	/// </summary>
	interface ILogger
	{
		/// <summary>
		/// Write an <see cref="Exception"/> to the log
		/// </summary>
		/// <param name="exception">The <see cref="Exception"/> that occurred</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task LogException(Exception exception);
	}
}
