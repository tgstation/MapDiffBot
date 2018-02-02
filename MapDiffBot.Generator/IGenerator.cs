using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Generator
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
		/// <param name="workingDirectory">The path that contains the .dme for the .dmms</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulthing in a <see cref="MapRegion"/> indicating differing segments of the map</returns>
		Task<MapRegion> GetDifferences(string mapPathA, string mapPathB, string workingDirectory, CancellationToken token);

		/// <summary>
		/// Get a <see cref="MapRegion"/> specifying the maximum size of a map
		/// </summary>
		/// <param name="mapPath">The path to the map. Must be in the proper place in it's codebase structure</param>
		/// <param name="workingDirectory">The path that contains the .dme for the .dmms</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulthing in a <see cref="MapRegion"/> specifying the maximum size of a map</returns>
		Task<MapRegion> GetMapSize(string mapPath, string workingDirectory, CancellationToken token);

		/// <summary>
		/// Render a map
		/// </summary>
		/// <param name="mapPath">The path to the map. Must be in the proper place in it's codebase structure</param>
		/// <param name="diffRegion">Optional region of the map to render</param>
		/// <param name="workingDirectory">The path that contains the .dme for the .dmm</param>
		/// <param name="outputDirectory">The path to the directory in which to store the output file</param>
		/// <param name="postfix">If not <see langword="null"/>, applies this in between output file map name and extension delimited by a "."</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulthing in a  path to the rendered .png in the output directory</returns>
		Task<string> RenderMap(string mapPath, MapRegion diffRegion, string workingDirectory, string outputDirectory, string postfix, CancellationToken token);
	}
}
