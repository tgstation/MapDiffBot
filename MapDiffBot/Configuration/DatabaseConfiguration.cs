namespace MapDiffBot.Configuration
{
	/// <summary>
	/// Database configuration settings
	/// </summary>
	sealed class DatabaseConfiguration
	{
		/// <summary>
		/// The configuration section the <see cref="DatabaseConfiguration"/> resides in
		/// </summary>
		public const string Section = "Database";

		/// <summary>
		/// <see langword="true"/> if <see cref="ConnectionString"/> is for MySQL, <see langword="false"/> if it's for SQL Server
		/// </summary>
		public bool IsMySQL { get; set; }

		/// <summary>
		/// The database connection string
		/// </summary>
		public string ConnectionString { get; set; }
	}
}
