namespace MapDiffBot.Configuration
{
	/// <summary>
	/// General configuration settings
	/// </summary>
    sealed class GeneralConfiguration
    {
		/// <summary>
		/// The configuration section the <see cref="GeneralConfiguration"/> resides in
		/// </summary>
		public const string Section = "General";

		/// <summary>
		/// The public URL for the application
		/// </summary>
		public string ApplicationPrefix { get; set; }
    }
}
