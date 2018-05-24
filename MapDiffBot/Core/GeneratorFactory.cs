using MapDiffBot.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
#pragma warning disable CA1812
	sealed class GeneratorFactory : IGeneratorFactory, IProcessThrottler, IDisposable
#pragma warning restore CA1812
	{
		/// <summary>
		/// The <see cref="GeneralConfiguration"/> for the <see cref="GeneratorFactory"/>
		/// </summary>
		readonly GeneralConfiguration generalConfiguration;

		/// <summary>
		/// The <see cref="SemaphoreSlim"/> for the <see cref="GeneratorFactory"/>
		/// </summary>
		readonly SemaphoreSlim semaphore;

		/// <summary>
		/// Construct a <see cref="GeneratorFactory"/>
		/// </summary>
		/// <param name="generalConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing the value of <see cref="generalConfiguration"/></param>
		public GeneratorFactory(IOptions<GeneralConfiguration> generalConfigurationOptions)
		{
			generalConfiguration = generalConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(generalConfigurationOptions));
			if(generalConfiguration.ProcessLimit > 0)
				semaphore = new SemaphoreSlim((int)generalConfiguration.ProcessLimit);
		}

		/// <inheritdoc />
		public void Dispose() => semaphore?.Dispose();

		/// <inheritdoc />
		public async Task<IDisposable> BeginProcess(CancellationToken cancellationToken) => semaphore == null ? (IDisposable)new NoOpDisposable() : await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false);

		/// <inheritdoc />
		public IGenerator CreateGenerator(string dmeToUse, IIOManager ioManager) => new Generator(dmeToUse, ioManager, this);
	}
}
