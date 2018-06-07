using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MapDiffBot.Models
{
	/// <summary>
	/// Represents a map diff set
	/// </summary>
    public sealed class MapDiff
	{
		/// <summary>
		/// The <see cref="Octokit.Repository.Id"/>
		/// </summary>
		[Key, Column(Order = 0)]
		[Required]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public long InstallationRepositoryId { get; set; }

		/// <summary>
		/// The <see cref="Octokit.CheckRun.Id"/>
		/// </summary>
		[Key, Column(Order = 1)]
		[Required]
		public long CheckRunId { get; set; }

		/// <summary>
		/// The id of the diffed file
		/// </summary>
		[Key, Column(Order = 2)]
		[Required]
		public int FileId { get; set; }

		/// <summary>
		/// The path of the diffed files
		/// </summary>
		[Required]
		public string MapPath { get; set; }

		/// <summary>
		/// The before <see cref="Image"/>
		/// </summary>
		public Image BeforeImage { get; set; }

		/// <summary>
		/// The after <see cref="Image"/>
		/// </summary>
		public Image AfterImage { get; set; }

		/// <summary>
		/// The difference <see cref="Image"/>
		/// </summary>
		public Image DifferenceImage { get; set; }

		/// <summary>
		/// Logs of the operation
		/// </summary>
		[Required]
		public string LogMessage { get; set; }
	}
}
