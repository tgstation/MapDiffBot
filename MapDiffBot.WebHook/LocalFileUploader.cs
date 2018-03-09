using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see cref="IFileUploader"/> for hosting locally
	/// </summary>
	sealed class LocalFileUploader : IFileUploader
	{
		/// <summary>
		/// The host portion of the URL from the last known HTTP request
		/// </summary>
		public static string LastKnownHost { private get; set; }

		/// <summary>
		/// If host portion of the URL from the last known HTTP request used HTTPS
		/// </summary>
		public static bool LastKnownHostIsHttps { private get; set; }

		/// <summary>
		/// The prefix path to remove from the path parameter of <see cref="Upload(string, string, CancellationToken)"/>
		/// </summary>
		static readonly string RemovePath = Path.Combine(Application.DataDirectory, "Requests", "MapDiffs", "Operations");

		/// <inheritdoc />
		public Task<string> Upload(string path, string apiKey, CancellationToken token)
		{
			return Task.FromResult(String.Format(CultureInfo.InvariantCulture, "http{0}://{1}", LastKnownHostIsHttps ? "s" : null, String.Concat(LastKnownHost, "/Operations", path.Replace(RemovePath, String.Empty).Replace(Path.DirectorySeparatorChar, '/'))));
		}
	}
}