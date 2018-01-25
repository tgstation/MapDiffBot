namespace MapDiffBot.Generator
{
	/// <inheritdoc />
	public sealed class GeneratorFactory : IGeneratorFactory
	{
		/// <inheritdoc />
		public IGenerator CreateGenerator(string dmePath)
		{
			return new DiffGenerator(dmePath);
		}
	}
}
