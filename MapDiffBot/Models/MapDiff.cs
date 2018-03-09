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
		public long RepositoryId { get; set; }

		/// <summary>
		/// The <see cref="Octokit.PullRequest.Number"/>
		/// </summary>
		[Key, Column(Order = 1)]
		[Required]
		public long PullRequestNumber { get; set; }

		/// <summary>
		/// The id of the diffed file
		/// </summary>
		[Key, Column(Order = 2)]
		[Required]
		public int FileId { get; set; }

		/// <summary>
		/// The <see cref="MapRegion"/> the diff was drawn over
		/// </summary>
		public MapRegion MapRegion { get; set; }

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
		/// Logs of the operation
		/// </summary>
		public string LogMessage { get; set; }
	}
}
