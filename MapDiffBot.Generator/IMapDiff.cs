namespace MapDiffBot.Generator
{
	/// <summary>
	/// Represents set of diff images
	/// </summary>
	public interface IMapDiff
	{
		/// <summary>
		/// The original name of the map file
		/// </summary>
		string OriginalMapName { get; }
		/// <summary>
		/// The path to the "before" image
		/// </summary>
		string BeforePath { get; }
		/// <summary>
		/// The path to the "after" image
		/// </summary>
		string AfterPath { get; }
	}
}
