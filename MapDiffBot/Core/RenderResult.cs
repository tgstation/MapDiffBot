using MapDiffBot.Models;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Represents the result of getting a <see cref="MapRegion"/> from dmm-tools
	/// </summary>
    public sealed class RenderResult : ToolResult
    {
		public string InputPath { get; set; }
		public string OutputPath { get; set; }
	}
}
