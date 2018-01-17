using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Imgur.API.Models;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
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
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, DefaultIOManager.DefaultBufferSize, true))
				image = await endpoint.UploadImageStreamAsync(fs);
			return image.Link;
		}
	}
}