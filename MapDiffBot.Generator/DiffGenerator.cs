using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Generator
{
	/// <inheritdoc />
	sealed class DiffGenerator : IGenerator
	{
		/// <summary>
		/// Generate the dmm-tools.exe command line arguments with the exception of the .dmm paramter which can be formatted in for rendering a map
		/// </summary>
		/// <param name="region">The <see cref="DiffRegion"/> to put on the command line, if any</param>
		/// <returns>The dmm-tools.exe command line arguments</returns>
		static string GenerateRenderCommandLine(DiffRegion region)
		{
			if (region == null)
				return "minimap --disable hide-space \"{0}\"";
			return String.Format(CultureInfo.InvariantCulture, "minimap --disable hide-space --min {0},{1},{2} --max {3},{4},{5} {{0}}", region.MinX, region.MinY, region.MinZ, region.MaxX, region.MaxY, region.MaxZ);
		}

		/// <summary>
		/// Extract the dmm-tools.exe from the running <see cref="System.Reflection.Assembly"/> and return the path to it
		/// </summary>
		static async Task<string> GetDMMToolsPath()
		{
			//TODO
			return await Task.FromResult("S:/Documents/Actual Documents/DA Git/SpacemanDMM/target/debug/dmm-tools.exe");
		}

		/// <summary>
		/// Creates a pre-configured <see cref="Process"/> pointing to dmm-tools.exe
		/// </summary>
		/// <param name="workingDirectory">The path of the map to render. Must be among associated codebase files</param>
		/// <param name="output">A <see cref="StringBuilder"/> used to accept stdout</param>
		/// <param name="errorOutput">A <see cref="StringBuilder"/> used to accept stderr</param>
		/// <returns>A <see cref="Task"/> resulting in a pre-configured <see cref="Process"/> pointing to dmm-tools.exe</returns>
		static async Task<Process> CreateDMMToolsProcess(string workingDirectory, StringBuilder output, StringBuilder errorOutput)
		{
			var P = new Process();
			P.StartInfo.RedirectStandardOutput = true;
			P.StartInfo.RedirectStandardError = true;
			P.StartInfo.UseShellExecute = false;
			P.StartInfo.WorkingDirectory = workingDirectory;
			P.OutputDataReceived += new DataReceivedEventHandler(
				delegate (object sender, DataReceivedEventArgs e)
				{
					output.Append(Environment.NewLine);
					output.Append(e.Data);
				}
			);
			P.ErrorDataReceived += new DataReceivedEventHandler(
				delegate (object sender, DataReceivedEventArgs e)
				{
					errorOutput.Append(Environment.NewLine);
					errorOutput.Append(e.Data);
				}
			);

			try
			{
				P.StartInfo.FileName = await GetDMMToolsPath();
			}
			catch
			{
				P.Dispose();
				throw;
			}

			return P;
		}

		/// <summary>
		/// Runs a <see cref="Process"/> asynchronously
		/// </summary>
		/// <param name="process">The <see cref="Process"/> to run</param>
		/// <param name="output">The stdout <see cref="StringBuilder"/></param>
		/// <param name="errorOutput">The stderr <see cref="StringBuilder"/></param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		static async Task StartAndWaitForProcessExit(Process process, StringBuilder output, StringBuilder errorOutput, CancellationToken token)
		{
			var tcs = new TaskCompletionSource<object>();
			process.EnableRaisingEvents = true;
			process.Exited += (a, b) =>
			{
				if (token.IsCancellationRequested)
					tcs.SetCanceled();
				else
					tcs.SetResult(null);
			};

			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			using (var reg = token.Register(() =>
			{
				try
				{
					process.Kill();
				}
				catch (InvalidOperationException) { }
			}))
				await tcs.Task;

			if (process.ExitCode != 0)
				throw new GeneratorException(String.Format(CultureInfo.CurrentCulture, "dmm-tools.exe exited with error code {0}!{1}Command line: {4}{1}Output:{1}{2}{1}Error:{1}{3}", process.ExitCode, Environment.NewLine, output, errorOutput, process.StartInfo.Arguments));
		}

		/// <summary>
		/// Renders the map at <paramref name="mapPath"/> with the given <paramref name="region"/>
		/// </summary>
		/// <param name="mapPath">The path of the map to render. Must be among associated codebase files</param>
		/// <param name="workingDirectory">The path that contains the .dme for the .dmm</param>
		/// <param name="region">The <see cref="DiffRegion"/> to render, if any</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in the path to the rendered .png file</returns>
		static async Task<string> RenderMap(string mapPath, string workingDirectory, DiffRegion region, CancellationToken token)
		{
			var output = new StringBuilder();
			var errorOutput = new StringBuilder();
			using (var P = await CreateDMMToolsProcess(workingDirectory, output, errorOutput))
			{
				P.StartInfo.Arguments = String.Format(CultureInfo.InvariantCulture, GenerateRenderCommandLine(region), mapPath);

				await StartAndWaitForProcessExit(P, output, errorOutput, token);
			}
			
			bool expectNext = false;
			foreach (var I in output.ToString().Split(' '))
			{
				var text = I.Trim();
				if (text == "saving")
					expectNext = true;
				else if (expectNext && text.EndsWith(".png"))
					return text;
				else
					expectNext = false;
			}

			throw new GeneratorException(String.Format(CultureInfo.CurrentCulture, "Unable to find .png file in dmm-tools output! Output:{0}{1}{0}Error:{0}{2}", Environment.NewLine, output.ToString(), errorOutput.ToString()));
		}

		/// <summary>
		/// Generate a <see cref="DiffRegion"/> given two map files with a common ancestor
		/// </summary>
		/// <param name="mapPathA">The path to the "before" map</param>
		/// <param name="mapPathB">The path to the "after" map</param>
		/// <param name="workingDirectory">The path that contains the .dme for the .dmms</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in a <see cref="DiffRegion"/> for the two maps</returns>
		static async Task<DiffRegion> GetDiffRegion(string mapPathA, string mapPathB, string workingDirectory, CancellationToken token)
		{
			if (mapPathA == null || mapPathB == null)
				//need a full rendering
				return null;
			
			var output = new StringBuilder();
			var errorOutput = new StringBuilder();
			using (var P = await CreateDMMToolsProcess(workingDirectory, output, errorOutput))
			{
				P.StartInfo.Arguments = String.Format(CultureInfo.InvariantCulture, "diff-maps \"{0}\" \"{1}\"", mapPathA, mapPathB);

				await StartAndWaitForProcessExit(P, output, errorOutput, token);
			}
			
			var matches = Regex.Matches(output.ToString(), "\\(([1-9][0-9]*), ([1-9][0-9]*), ([1-9][0-9]*)\\)");
			if (matches.Count == 0)
				return null;

			var region = new DiffRegion()
			{
				MinX = Int32.MaxValue,
				MinY = Int32.MaxValue,
				MinZ = Int32.MaxValue
			};

			try
			{
				foreach (Match I in matches)
				{
					region.MaxX = Math.Max(region.MaxX, Convert.ToInt32(I.Groups[1].Value));
					region.MinX = Math.Min(region.MinX, Convert.ToInt32(I.Groups[1].Value));

					region.MaxY = Math.Max(region.MaxY, Convert.ToInt32(I.Groups[2].Value));
					region.MinY = Math.Min(region.MinY, Convert.ToInt32(I.Groups[2].Value));

					region.MaxZ = Math.Max(region.MaxZ, Convert.ToInt32(I.Groups[3].Value));
					region.MinZ = Math.Min(region.MinZ, Convert.ToInt32(I.Groups[3].Value));
				}
			}
			catch (Exception e)
			{
				throw new GeneratorException("An exception occurred during diff processing!", e);
			}

			return region;
		}

		/// <summary>
		/// Clean up the extracted dmm-tools.exe
		/// </summary>
		public void Dispose()
		{
			//TODO
		}

		/// <inheritdoc />
		public async Task<IMapDiff> GenerateDiff(string mapPathA, string mapPathB, string workingDirectory, string outputDirectory, CancellationToken token)
		{
			var region = await GetDiffRegion(mapPathA, mapPathB, workingDirectory, token);

			Directory.CreateDirectory(Path.Combine(workingDirectory, "data", "minimaps"));
			
			var mA = RenderMap(mapPathA, workingDirectory, region, token);
			var mB = RenderMap(mapPathB, workingDirectory, region, token);

			var mapName = Path.GetFileNameWithoutExtension(mapPathA.Length > mapPathB.Length ? mapPathB : mapPathA);

			var outputA = Path.Combine(workingDirectory, await mA);
			var outputB = Path.Combine(workingDirectory, await mB);

			var movedA = Path.Combine(outputDirectory, String.Format(CultureInfo.InvariantCulture, "{0}.before.png", mapName));
			var movedB = Path.Combine(outputDirectory, String.Format(CultureInfo.InvariantCulture, "{0}.after.png", mapName));

			File.Move(outputA, movedA);
			File.Move(outputB, movedB);

			return new MapDiff(mapName, movedA, movedB);
		}
	}
}
