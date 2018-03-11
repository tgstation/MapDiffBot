using MapDiffBot.Models;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Represents the result of getting a <see cref="MapRegion"/> from dmm-tools
	/// </summary>
    public sealed class RenderResult : ToolResult
    {
		/// <summary>
		/// The original path of the map
		/// </summary>
		public string InputPath { get; set; }
		/// <summary>
		/// The path the output png was placed at
		/// </summary>
		public string OutputPath { get; set; }
	}
}
