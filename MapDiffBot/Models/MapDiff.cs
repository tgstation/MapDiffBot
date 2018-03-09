using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MapDiffBot.Models
{
    public sealed class MapDiff
	{
		[Key, Column(Order = 0)]
		[Required]
		public long RepositoryId { get; set; }

		[Key, Column(Order = 1)]
		[Required]
		public long PullRequestNumber { get; set; }

		[Key, Column(Order = 2)]
		[Required]
		public int FileId { get; set; }

		public MapRegion MapRegion { get; set; }

		[Required]
		public string MapPath { get; set; }
		
		public Image BeforeImage { get; set; }
		
		public Image AfterImage { get; set; }

		public string ErrorMessage { get; set; }
	}
}
