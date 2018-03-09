using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Web request provider
	/// </summary>
	public interface IWebRequestManager
	{
		/// <summary>
		/// Run a HTTP GET on a given <paramref name="url"/>
		/// </summary>
		/// <param name="url">The <see cref="Uri"/> of the request</param>
		/// <param name="headers">HTTP headers of the request</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the body of the response of the request</returns>
		Task<string> RunGet(Uri url, IEnumerable<string> headers, CancellationToken cancellationToken);
	}
}
