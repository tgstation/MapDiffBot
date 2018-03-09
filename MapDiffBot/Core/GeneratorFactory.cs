namespace MapDiffBot.Core
{
	/// <inheritdoc />
	sealed class GeneratorFactory : IGeneratorFactory
	{
		/// <inheritdoc />
		public IGenerator CreateGenerator(string dmeToUse, IIOManager ioManager) => new Generator(dmeToUse, ioManager);
	}
}
