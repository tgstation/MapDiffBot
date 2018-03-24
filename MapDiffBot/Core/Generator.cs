using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
{
	/// <inheritdoc />
	sealed class Generator : IGenerator
	{
		/// <summary>
		/// Path to dmm-tools.exe
		/// </summary>
		const string DMMToolsPath = "./dmm-tools.exe";

		/// <summary>
		/// Path to the .dme to pass to dmm-tools
		/// </summary>
		readonly string dmeArgument;
		/// <summary>
		/// Path to the .dme to pass to dmm-tools
		/// </summary>
		readonly IIOManager ioManager;

		/// <summary>
		/// Construct a <see cref="Generator"/>
		/// </summary>
		/// <param name="dmePath">Used for creating the <see cref="dmeArgument"/></param>
		/// <param name="ioManager">The value of <see cref="ioManager"/></param>
		public Generator(string dmePath, IIOManager ioManager)
		{
			dmeArgument = dmePath != null ? String.Format(CultureInfo.InvariantCulture, "-e \"{0}\" ", Path.GetFileName(dmePath)) : null;
			this.ioManager = ioManager ?? throw new ArgumentNullException(nameof(ioManager));
		}

		/// <summary>
		/// Generate the dmm-tools.exe command line arguments with the exception of the .dmm paramter which can be formatted in for rendering a map
		/// </summary>
		/// <param name="region">The <see cref="MapRegion"/> to put on the command line, if any</param>
		/// <returns>The dmm-tools.exe command line arguments</returns>
		string GenerateRenderCommandLine(MapRegion region)
		{
			if (region == null)
				return String.Format(CultureInfo.InvariantCulture, "{0}minimap --disable hide-space,hide-invisible,random \"{{0}}\"", dmeArgument);
			return String.Format(CultureInfo.InvariantCulture, "{6}minimap --disable hide-space,hide-invisible,random --min {0},{1},{2} --max {3},{4},{5} \"{{0}}\"", region.MinX, region.MinY, 1, region.MaxX, region.MaxY, 1, dmeArgument);
		}

		/// <summary>
		/// Creates a pre-configured <see cref="Process"/> pointing to dmm-tools.exe
		/// </summary>
		/// <param name="output">A <see cref="StringBuilder"/> used to accept stdout</param>
		/// <param name="errorOutput">A <see cref="StringBuilder"/> used to accept stderr</param>
		/// <returns>A <see cref="Task"/> resulting in a pre-configured <see cref="Process"/> pointing to dmm-tools.exe</returns>
		Process CreateDMMToolsProcess(StringBuilder output, StringBuilder errorOutput)
		{
			var P = new Process();
			P.StartInfo.RedirectStandardOutput = true;
			P.StartInfo.RedirectStandardError = true;
			P.StartInfo.UseShellExecute = false;
			P.StartInfo.WorkingDirectory = ioManager.ResolvePath(".");
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
				P.StartInfo.FileName = DMMToolsPath;
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
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		static async Task<int> StartAndWaitForProcessExit(Process process, CancellationToken cancellationToken)
		{
			var tcs = new TaskCompletionSource<object>();
			process.EnableRaisingEvents = true;
			process.Exited += (a, b) =>
			{
				if (cancellationToken.IsCancellationRequested)
					tcs.SetCanceled();
				else
					tcs.SetResult(null);
			};

			process.Start();
			try
			{
				process.PriorityClass = ProcessPriorityClass.BelowNormal;
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();
				using (var reg = cancellationToken.Register(() =>
				{
					try
					{
						process.Kill();
					}
					catch (InvalidOperationException) { }
				}))
					await tcs.Task.ConfigureAwait(false);
			}
			//process exited
			catch (InvalidOperationException) { }

			return process.ExitCode;
		}

		/// <inheritdoc />
		public async Task<ToolResult> GetMapSize(string mapPath, CancellationToken cancellationToken)
		{
			if (mapPath == null)
				throw new ArgumentNullException(nameof(mapPath));

			string mapName;
			var output = new StringBuilder();
			var errorOutput = new StringBuilder();
			var args = String.Format(CultureInfo.InvariantCulture, "{0}map-info -j \"{1}\"", dmeArgument, mapPath);
			Task<int> processTask;
			using (var P = CreateDMMToolsProcess(output, errorOutput))
			{
				P.StartInfo.Arguments = args;

				processTask = StartAndWaitForProcessExit(P, cancellationToken);

				mapName = Path.GetFileNameWithoutExtension(mapPath);

				await processTask.ConfigureAwait(false);
			}

			var toolOutput = String.Format(CultureInfo.InvariantCulture, "Exit Code: {0}{1}StdOut:{1}{2}{1}StdErr:{1}{3}", processTask.Result, Environment.NewLine, output, errorOutput);
			var result = new ToolResult { ToolOutput = toolOutput, CommandLine = args };
			try
			{
				var json = JsonConvert.DeserializeObject<IDictionary<string, IDictionary<string, object>>>(output.ToString());
				var map = json[mapPath];
				var size = (JArray)map["size"];
				result.MapRegion = new MapRegion
				{
					MinX = 1,
					MinY = 1,
					MaxX = (short)size[0],
					MaxY = (short)size[1]
				};
			}
			catch { }
			return result;
		}

		/// <inheritdoc />
		public async Task<RenderResult> RenderMap(string mapPath, MapRegion region, string outputDirectory, string postfix, CancellationToken cancellationToken)
		{
			if (mapPath == null)
				throw new ArgumentNullException(nameof(mapPath));
			if (outputDirectory == null)
				throw new ArgumentNullException(nameof(outputDirectory));
			if (postfix == null)
				throw new ArgumentNullException(nameof(postfix));

			var mapName = ioManager.GetFileNameWithoutExtension(mapPath);
			var outFile = ioManager.ConcatPath(outputDirectory, String.Format(CultureInfo.InvariantCulture, "{0}.{1}png", mapName, postfix != null ? String.Concat(postfix, '.') : null));
			var args = String.Format(CultureInfo.InvariantCulture, GenerateRenderCommandLine(region), mapPath);
			
			await ioManager.CreateDirectory(ioManager.ConcatPath("data", "minimaps"), cancellationToken).ConfigureAwait(false);

			var output = new StringBuilder();
			var errorOutput = new StringBuilder();
			Task<int> processTask;
			using (var P = CreateDMMToolsProcess(output, errorOutput))
			{
				P.StartInfo.Arguments = args;

				processTask = StartAndWaitForProcessExit(P, cancellationToken);

				await processTask.ConfigureAwait(false);
			}
			var toolOutput = String.Format(CultureInfo.InvariantCulture, "Exit Code: {0}{1}StdOut:{1}{2}{1}StdErr:{1}{3}", processTask.Result, Environment.NewLine, output, errorOutput);

			bool expectNext = false;
			string result = null;
			foreach (var I in output.ToString().Split(' '))
			{
				var text = I.Trim();
				if (text == "saving")
					expectNext = true;
				else if (expectNext && text.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase))
				{
					result = text;
					break;
				}
				else
					expectNext = false;
			}
			var rresult = new RenderResult
			{
				MapRegion = region,
				CommandLine = args,
				ToolOutput = toolOutput,
				InputPath = mapPath
			};

			if (result != null)
			{
				await ioManager.MoveFile(result, outFile, cancellationToken).ConfigureAwait(false);
				rresult.OutputPath = ioManager.ResolvePath(outFile);
			}

			return rresult;
		}

		/// <inheritdoc />
		public async Task<ToolResult> GetDifferences(string mapPathA, string mapPathB, CancellationToken cancellationToken)
		{
			if (mapPathA == null)
				throw new ArgumentNullException(nameof(mapPathA));
			if (mapPathB == null)
				throw new ArgumentNullException(nameof(mapPathB));

			var output = new StringBuilder();
			var errorOutput = new StringBuilder();
			var args = String.Format(CultureInfo.InvariantCulture, "{2}diff-maps \"{0}\" \"{1}\"", mapPathA, mapPathB, dmeArgument);
			Task<int> processTask;
			using (var P = CreateDMMToolsProcess(output, errorOutput))
			{
				P.StartInfo.Arguments = args;

				processTask = StartAndWaitForProcessExit(P, cancellationToken);
				await processTask.ConfigureAwait(false);
			}
			var toolOutput = String.Format(CultureInfo.InvariantCulture, "Exit Code: {0}{1}StdOut:{1}{2}{1}StdErr:{1}{3}", processTask.Result, Environment.NewLine, output, errorOutput);

			var result = new ToolResult
			{
				CommandLine = args,
				ToolOutput = toolOutput
			};

			var matches = Regex.Matches(output.ToString(), "\\(([1-9][0-9]*), ([1-9][0-9]*), ([1-9][0-9]*)\\)");
			if (matches.Count == 0)
				return result;

			var region = new MapRegion()
			{
				MinX = Int16.MaxValue,
				MinY = Int16.MaxValue
			};

			try
			{
				foreach (Match I in matches)
				{
					region.MaxX = Math.Max(region.MaxX, Convert.ToInt16(I.Groups[1].Value, CultureInfo.InvariantCulture));
					region.MinX = Math.Min(region.MinX, Convert.ToInt16(I.Groups[1].Value, CultureInfo.InvariantCulture));

					region.MaxY = Math.Max(region.MaxY, Convert.ToInt16(I.Groups[2].Value, CultureInfo.InvariantCulture));
					region.MinY = Math.Min(region.MinY, Convert.ToInt16(I.Groups[2].Value, CultureInfo.InvariantCulture));
				}
				result.MapRegion = region;
			}
			catch { }

			return result;
		}
	}
}