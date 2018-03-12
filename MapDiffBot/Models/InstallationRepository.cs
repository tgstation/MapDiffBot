using System.Collections.Generic;
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
		/// The <see cref="Octokit.Repository.Id"/>
		/// </summary>
		[Required]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public long Id { get; set; }

		/// <summary>
		/// Path to the .dme for the generator to run on
		/// </summary>
		public string TargetDme { get; set; }

		/// <summary>
		/// The <see cref="MapDiffs"/> in the <see cref="InstallationRepository"/>
		/// </summary>
#pragma warning disable CA2227 // Collection properties should be read only
		public List<MapDiff> MapDiffs { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
	}
}
