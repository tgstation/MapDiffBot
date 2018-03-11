using System;
using System.Globalization;

namespace MapDiffBot.Models
{
	/// <summary>
	/// Represents a region on a .dmm map
	/// </summary>
	public sealed class MapRegion
	{
		/// <summary>
		/// Column Id
		/// </summary>
		[Obsolete("For use by EFCore only", true)]
		public int Id { get; set; }

		/// <summary>
		/// The X coordinate of the bottom left turf
		/// </summary>
		public short MinX { get; set; }

		/// <summary>
		/// The Y coordinate of the bottom left turf
		/// </summary>
		public short MinY { get; set; }
		
		/// <summary>
		/// The X coordinate of the top right turf
		/// </summary>
		public short MaxX { get; set; }

		/// <summary>
		/// The Y coordinate of the top right turf
		/// </summary>
		public short MaxY { get; set; }

		/// <inheritdoc />
		public override string ToString() => String.Format(CultureInfo.InvariantCulture, "({0}, {1}) => ({2}, {3})", MinX, MinY, MaxX, MaxY);
	}
}
