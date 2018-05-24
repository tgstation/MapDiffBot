using System;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Provides a no-op <see cref="IDisposable"/> implementation
	/// </summary>
	sealed class NoOpDisposable : IDisposable
	{
		/// <inheritdoc />
		public void Dispose() { }
	}
}
