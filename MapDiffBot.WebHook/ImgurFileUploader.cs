using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Imgur.API.Models;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see cref="IFileUploader"/> for imgur
	/// </summary>
	[SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Imgur")]
	public class ImgurFileUploader : IFileUploader
	{
		/// <summary>
		/// Maximum number of retries for an upload
		/// </summary>
		const int MaxRetries = 10;

		/// <inheritdoc />
		public async Task<string> Upload(string path, string apiKey, CancellationToken token)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			if (apiKey == null)
				throw new ArgumentNullException(nameof(apiKey));
			var splits = apiKey.Split('/');
			if (splits.Length < 2)
				throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, "Invalid {0} {1}!", nameof(ImgurFileUploader), nameof(apiKey)));

			var endpoint = new ImageEndpoint(new ImgurClient(splits[0], splits[1]));
			IImage image;
			for (var I = 1; ; ++I)
				try
				{
					using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, DefaultIOManager.DefaultBufferSize, true))
						image = await endpoint.UploadImageStreamAsync(fs);
					break;
				}
				catch (WebException)
				{
					//try again a few times because api.imgur.com can just choose to not resolve for no bloody reason
					if (I > MaxRetries)
						throw;

					await Task.Delay(I * 1000, token);
					token.ThrowIfCancellationRequested();
				}
			return image.Link;
		}
	}
}