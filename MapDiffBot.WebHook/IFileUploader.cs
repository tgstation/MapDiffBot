using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see langword="interface"/> for uploading files
	/// </summary>
	interface IFileUploader
	{
		/// <summary>
		/// Upload a <paramref name="path"/>>
		/// </summary>
		/// <param name="path">Path to the file to upload</param>
		/// <param name="apiKey">API key used to access the uploader. May be <see langword="null"/></param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the URL that may be used to access the uploaded <paramref name="path"/></returns>
		Task<string> Upload(string path, string apiKey, CancellationToken token);
	}
}
