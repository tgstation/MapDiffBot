using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see cref="IHttpActionResult"/> for returning files
	/// </summary>
	sealed class FileResult : IHttpActionResult
	{
		/// <summary>
		/// The path to the file to retrieve
		/// </summary>
		readonly string filePath;

		/// <summary>
		/// Construct a <see cref="FileResult"/>
		/// </summary>
		/// <param name="_filePath">The value of <see cref="filePath"/></param>
		public FileResult(string _filePath)
		{
			filePath = _filePath;
		}
		
		/// <inheritdoc />
		public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken token)
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new ByteArrayContent(await new DefaultIOManager().ReadAllBytes(filePath, token))
			};
			try
			{
				var contentType = MimeMapping.GetMimeMapping(Path.GetExtension(filePath));
				response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

				return response;
			}
			catch
			{
				response.Dispose();
				throw;
			}
		}
	}
}