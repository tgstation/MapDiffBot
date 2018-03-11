using System;

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
		public Guid Id { get; set; }

		/// <summary>
		/// The GitHub access token
		/// </summary>
		public string AccessToken { get; set; }

		/// <summary>
		/// When the represented cookie expires
		/// </summary>
		public DateTimeOffset Expiry { get; set; }
	}
}
