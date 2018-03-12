using System;
using System.ComponentModel.DataAnnotations;

namespace MapDiffBot.Models
{
	/// <summary>
	/// Describes a GitHub AccessToken stored within a cookie
	/// </summary>
	public sealed class UserAccessToken
	{
		/// <summary>
		/// The identifier for the cookie
		/// </summary>
		[Required]
		public Guid Id { get; set; }

		/// <summary>
		/// The GitHub access token
		/// </summary>
		[Required]
		public string AccessToken { get; set; }

		/// <summary>
		/// When the represented cookie expires
		/// </summary>
		[Required]
		public DateTimeOffset Expiry { get; set; }
	}
}
