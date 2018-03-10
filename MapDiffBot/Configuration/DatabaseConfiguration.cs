namespace MapDiffBot.Configuration
{
	/// <summary>
	/// Database configuration settings
	/// </summary>
#pragma warning disable CA1812
	sealed class DatabaseConfiguration
#pragma warning restore CA1812
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
