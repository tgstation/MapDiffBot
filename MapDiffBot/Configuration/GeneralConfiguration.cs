namespace MapDiffBot.Configuration
{
	/// <summary>
	/// General configuration settings
	/// </summary>
	public sealed class GeneralConfiguration
	{
		/// <summary>
		/// The configuration section the <see cref="GeneralConfiguration"/> resides in
		/// </summary>
		public const string Section = "General";

		/// <summary>
		/// The public URL for the application
		/// </summary>
		public string ApplicationPrefix { get; set; }

		/// <summary>
		/// Maximum number of dmm-tools processes to run in tandem across all jobs
		/// </summary>
		public uint ProcessLimit { get; set; }
	}
}
