using System.ComponentModel.DataAnnotations;

namespace MapDiffBot.Models
{
	/// <summary>
	/// Represents image bytes
	/// </summary>
    public class Image
    {
		/// <summary>
		/// The row Id
		/// </summary>
		public long Id { get; set; }

		/// <summary>
		/// The <see cref="Image"/> bytes
		/// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
		[Required]
		public byte[] Data { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
	}
}
