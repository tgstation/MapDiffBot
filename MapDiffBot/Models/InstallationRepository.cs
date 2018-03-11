using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MapDiffBot.Models
{
	/// <summary>
	/// Represents a <see cref="Octokit.Repository"/> in an <see cref="Installation"/>
	/// </summary>
	public sealed class InstallationRepository
	{
		/// <summary>
		/// The Column ID
		/// </summary>
		[Key, Column(Order = 0)]
		public long ColumnId { get; set; }

		/// <summary>
		/// The <see cref="Octokit.Repository.Id"/>
		/// </summary>
		[Key, Column(Order = 1)]
		public long Id { get; set; }

		/// <summary>
		/// Path to the .dme for the generator to run on
		/// </summary>
		public string TargetDme { get; set; }
	}
}
