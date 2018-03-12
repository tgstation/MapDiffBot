using MapDiffBot.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <summary>
	/// Used for generating cropped comparison images of .dmm files
	/// </summary>
	public interface IGenerator : IDisposable
	{
		/// <summary>
		/// Gets a <see cref="MapRegion"/> indicating the differences between two maps
		/// </summary>
		/// <param name="mapPathA">The path to the first map</param>
		/// <param name="mapPathB">The path to the second map</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulthing in the <see cref="ToolResult"/> for the maps</returns>
		Task<ToolResult> GetDifferences(string mapPathA, string mapPathB, CancellationToken cancellationToken);

		/// <summary>
		/// Get a <see cref="MapRegion"/> specifying the maximum size of a map
		/// </summary>
		/// <param name="mapPath">The path to the map. Must be in the proper place in it's codebase structure</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the <see cref="ToolResult"/> indicating the size of <paramref name="mapPath"/></returns>
		Task<ToolResult> GetMapSize(string mapPath, CancellationToken cancellationToken);

		/// <summary>
		/// Render a map
		/// </summary>
		/// <param name="mapPath">The path to the map. Must be in the proper place in it's codebase structure</param>
		/// <param name="diffRegion">Optional region of the map to render</param>
		/// <param name="outputDirectory">The path to the directory in which to store the output file</param>
		/// <param name="postfix">If not <see langword="null"/>, applies this in between output file map name and extension delimited by a "."</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the <see cref="RenderResult"/></returns>
		Task<RenderResult> RenderMap(string mapPath, MapRegion diffRegion, string outputDirectory, string postfix, CancellationToken cancellationToken);
	}
}
