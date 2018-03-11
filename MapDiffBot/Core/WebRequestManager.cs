using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using StreamReader = System.IO.StreamReader;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
#pragma warning disable CA1812
	sealed class WebRequestManager : IWebRequestManager
#pragma warning restore CA1812
	{
		/// <summary>
		/// The <see cref="ILogger{TCategoryName}"/> for the <see cref="WebRequestManager"/>
		/// </summary>
		readonly ILogger<WebRequestManager> logger;

		/// <summary>
		/// Construct a <see cref="WebRequestManager"/>
		/// </summary>
		/// <param name="logger">The value of <see cref="logger"/></param>
		public WebRequestManager(ILogger<WebRequestManager> logger) => this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

		/// <inheritdoc />
		public async Task<string> RunGet(Uri url, IEnumerable<string> headers, CancellationToken cancellationToken)
		{
			if (url == null)
				throw new ArgumentNullException(nameof(url));
			if (headers == null)
				throw new ArgumentNullException(nameof(headers));

			logger.LogTrace("GET: {0}, Headers: {1}", url, String.Join(';', headers));

			var request = WebRequest.Create(url);
			request.Method = "GET";
			foreach (var I in headers)
				request.Headers.Add(I);

			try
			{
				WebResponse response;
				using (cancellationToken.Register(() => request.Abort()))
					response = await request.GetResponseAsync().ConfigureAwait(false);

				string result;
				using (var reader = new StreamReader(response.GetResponseStream()))
					result = await reader.ReadToEndAsync().ConfigureAwait(false);

				logger.LogTrace("Request success.");
				return result;
			}
			catch (Exception e)
			{
				if (cancellationToken.IsCancellationRequested)
					throw new OperationCanceledException("RunRequest() cancelled!", e, cancellationToken);
				logger.LogWarning(e, "Request failed!");
				throw;
			}
		}
	}
}
