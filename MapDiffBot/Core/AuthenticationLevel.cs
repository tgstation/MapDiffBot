namespace MapDiffBot.Core
{
	/// <summary>
	/// Represents the authentication level of a user
	/// </summary>
	public enum AuthenticationLevel
	{
		/// <summary>
		/// The user isn't logged in
		/// </summary>
		LoggedOut,
		/// <summary>
		/// The user is logged in but is not a repository <see cref="Maintainer"/>
		/// </summary>
		User,
		/// <summary>
		/// The user is a repository maintainer
		/// </summary>
		Maintainer
	}
}
