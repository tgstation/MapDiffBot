using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see langword="interface"/> for handling various log messages
	/// </summary>
	interface ILogger
	{
		/// <summary>
		/// Write an error message to the log
		/// </summary>
		/// <param name="message">The error message</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task LogError(string message);
	}
}
