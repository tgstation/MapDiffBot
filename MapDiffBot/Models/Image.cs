using System.ComponentModel.DataAnnotations;

namespace MapDiffBot.Models
{
	/// <summary>
	/// Represents a .png image
	/// </summary>
	public sealed class Image
	{
		/// <summary>
		/// The column ID
		/// </summary>
		public int Id { get; set; }

		/// <summary>
		/// The image <see langword="byte"/>s
		/// </summary>
		[Required]
#pragma warning disable CA1819 // Properties should not return arrays
		public byte[] Data { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
	}
}
