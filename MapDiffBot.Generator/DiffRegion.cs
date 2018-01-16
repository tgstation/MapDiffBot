namespace MapDiffBot.Generator
{
	/// <summary>
	/// Represents a region on a .dmm map to create a diff for
	/// </summary>
	sealed class DiffRegion
	{
		/// <summary>
		/// The X coordinate of the bottom left turf
		/// </summary>
		public int MinX { get; set; }
		/// <summary>
		/// The Y coordinate of the bottom left turf
		/// </summary>
		public int MinY { get; set; }
		/// <summary>
		/// The X coordinate of the top right turf
		/// </summary>
		public int MaxX { get; set; }
		/// <summary>
		/// The Y coordinate of the top right turf
		/// </summary>
		public int MaxY { get; set; }
	}
}
