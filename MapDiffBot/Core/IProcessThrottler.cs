using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	interface IProcessThrottler
	{
		/// <summary>
		/// Call this before starting a dmm-tools process
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="IDisposable"/> that should be disposed once the process finishes</returns>
		Task<IDisposable> BeginProcess(CancellationToken cancellationToken);
	}
}