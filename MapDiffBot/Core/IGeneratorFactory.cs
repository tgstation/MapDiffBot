namespace MapDiffBot.Core
{
	/// <summary>
	/// Factory for creating <see cref="IGenerator"/>s
	/// </summary>
	interface IGeneratorFactory
	{
		/// <summary>
		/// Create a <see cref="IGenerator"/>
		/// </summary>
		/// <param name="dmeToUse">The .dme file to use as a root</param>
		/// <param name="ioManager">The <see cref="IIOManager"/> directory for the operations</param>
		/// <returns>A new <see cref="IGenerator"/></returns>
		IGenerator CreateGenerator(string dmeToUse, IIOManager ioManager);
	}
}
