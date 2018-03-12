namespace MapDiffBot.Core
{
	/// <summary>
	/// Result of a call to a <see cref="IGenerator"/>
	/// </summary>
	class ToolResult
	{
		/// <summary>
		/// Output of the tool
		/// </summary>
		public string ToolOutput { get; set; }

		/// <summary>
		/// Command line of the tool
		/// </summary>
		public string CommandLine { get; set; }

		/// <summary>
		/// The <see cref="MapRegion"/> for the operation
		/// </summary>
		public MapRegion MapRegion { get; set; }
	}
}