namespace MapDiffBot.Generator
{
	/// <summary>
	/// Factory for creating <see cref="IGenerator"/>s
	/// </summary>
	public interface IGeneratorFactory
	{
		/// <summary>
		/// Create a <see cref="IGenerator"/>
		/// </summary>
		/// <returns>A new <see cref="IGenerator"/></returns>
		IGenerator CreateGenerator();
	}
}
