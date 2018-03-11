using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MapDiffBot.Models
{
	/// <summary>
	/// Represents a <see cref="Octokit.Installation"/>
	/// </summary>
	public sealed class Installation
	{
		/// <summary>
		/// Primary key for the entity
		/// </summary>
		[Required]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public long Id { get; set; }

		/// <summary>
		/// The oauth access token for the <see cref="Installation"/>
		/// </summary>
		[Required]
		public string AccessToken { get; set; }

		/// <summary>
		/// When <see cref="AccessToken"/> expires
		/// </summary>
		[Required]
		public DateTimeOffset AccessTokenExpiry { get; set; }

		/// <summary>
		/// The .dme file to use for operations
		/// </summary>
		public string DefaultDme { get; set; }

		/// <summary>
		/// The <see cref="InstallationRepository"/>s in the <see cref="Installation"/>
		/// </summary>
#pragma warning disable CA2227 // Collection properties should be read only
		public List<InstallationRepository> Repositories { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
	}
}
