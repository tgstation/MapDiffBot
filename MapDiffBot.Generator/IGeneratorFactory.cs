using System.Diagnostics.CodeAnalysis;

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
		/// <param name="dmePath">Path to the .dme the created <see cref="IGenerator"/> will be using</param>
		/// <returns>A new <see cref="IGenerator"/></returns>
		[SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "dme")]
		IGenerator CreateGenerator(string dmePath);
	}
}
