using MapDiffBot.Core;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Threading.Tasks;

namespace MapDiffBot
{
	/// <summary>
	/// Entry <see langword="class"/> for the application
	/// </summary>
	static class Program
	{
		/// <summary>
		/// The <see cref="Func{T, TResult}"/> taking command line arguments and returning the <see cref="IWebHostBuilder"/> for the <see cref="Program"/>
		/// </summary>
		static readonly Func<string[], IWebHostBuilder> GetWebHostBuilder = args => WebHost.CreateDefaultBuilder(args);

		/// <summary>
		/// Entry point for the <see cref="Program"/>
		/// </summary>
		/// <param name="args">The command line arguments</param>
		/// <returns>A <see cref="Task"/> representing the scope of the <see cref="Program"/></returns>
		public static async Task Main(string[] args)
		{
			using (var webHost = GetWebHostBuilder(args).UseStartup<Application>().Build())
				await webHost.RunAsync().ConfigureAwait(false);
		}
	}
}
