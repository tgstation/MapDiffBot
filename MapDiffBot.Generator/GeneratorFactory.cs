namespace MapDiffBot.Generator
{
	/// <inheritdoc />
	public sealed class GeneratorFactory : IGeneratorFactory
	{
		/// <inheritdoc />
		public IGenerator CreateGenerator()
		{
			return new DiffGenerator();
		}
	}
}
