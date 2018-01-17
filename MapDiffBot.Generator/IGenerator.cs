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
		/// Generate one or two cropped comparison images of .dmm file
		/// </summary>
		/// <param name="mapPathA">The path to the "before" map. Must be in the proper place in it's codebase structure. May be <see langword="null"/> if <paramref name="mapPathB"/> isn't</param>
		/// <param name="mapPathB">The path to the "after" map. Must be in the proper place in it's codebase structure. May be <see langword="null"/> if <paramref name="mapPathA"/> isn't</param>
		/// <param name="workingDirectory">The path that contains the .dme for the .dmms</param>
		/// <param name="outputDirectory">The path to the directory in which to store the output files</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns></returns>
		Task<IMapDiff> GenerateDiff(string mapPathA, string mapPathB, string workingDirectory, string outputDirectory, CancellationToken token);
	}
}
