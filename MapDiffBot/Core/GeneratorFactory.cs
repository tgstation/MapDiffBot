namespace MapDiffBot.Core
{
	/// <inheritdoc />
#pragma warning disable CA1812
	sealed class GeneratorFactory : IGeneratorFactory
#pragma warning restore CA1812
	{
		/// <inheritdoc />
		public IGenerator CreateGenerator(string dmeToUse, IIOManager ioManager) => new Generator(dmeToUse, ioManager);
	}
}
