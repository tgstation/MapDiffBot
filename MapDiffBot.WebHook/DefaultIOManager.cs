using System;
using System.Collections.Generic;
using System.IO;
#if LARGE_FILE_SUPPORT
using System.Linq;
#endif
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see cref="IIOManager"/> that resolves paths to <see cref="Environment.CurrentDirectory"/>
	/// </summary>
	class DefaultIOManager : IIOManager
	{
		/// <summary>
		/// Default <see cref="FileStream"/> buffer size used by .NET
		/// </summary>
		public const int DefaultBufferSize = 4096;

		/// <summary>
		/// Recursively empty a directory
		/// </summary>
		/// <param name="dir"><see cref="DirectoryInfo"/> of the directory to empty</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		static async Task NormalizeAndDelete(DirectoryInfo dir, CancellationToken token)
		{
			var tasks = new List<Task>();

			foreach (var subDir in dir.EnumerateDirectories())
			{
				token.ThrowIfCancellationRequested();
				tasks.Add(NormalizeAndDelete(subDir, token));
			}
			foreach (var file in dir.EnumerateFiles())
			{
				token.ThrowIfCancellationRequested();
				file.Attributes = FileAttributes.Normal;
				file.Delete();
			}
			token.ThrowIfCancellationRequested();
			await Task.WhenAll(tasks);
			token.ThrowIfCancellationRequested();
			dir.Delete(true);
		}

		/// <inheritdoc />
		public async Task AppendAllText(string path, string additional_contents, CancellationToken token)
		{
			if (additional_contents == null)
				throw new ArgumentNullException(nameof(additional_contents));
			using (var destStream = new FileStream(ResolvePath(path), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, DefaultBufferSize, true))
			{
				var buf = Encoding.UTF8.GetBytes(additional_contents);
				await destStream.WriteAsync(buf, 0, buf.Length, token);
			}
		}

		/// <inheritdoc />
		public string ConcatPath(params string[] paths)
		{
			if (paths == null)
				throw new ArgumentNullException(nameof(paths));
			return Path.Combine(paths);
		}

		/// <inheritdoc />
		public async Task CopyFile(string src, string dest, CancellationToken token)
		{
			if (src == null)
				throw new ArgumentNullException(nameof(src));
			if (dest == null)
				throw new ArgumentNullException(nameof(dest));
			using (var srcStream = new FileStream(ResolvePath(src), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, DefaultBufferSize, true))
			using (var destStream = new FileStream(ResolvePath(dest), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, DefaultBufferSize, true))
				await srcStream.CopyToAsync(destStream, DefaultBufferSize, token);
		}

		/// <inheritdoc />
		public Task CreateDirectory(string path, CancellationToken token)
		{
			return Task.Run(() => Directory.CreateDirectory(ResolvePath(path)), token);
		}

		/// <inheritdoc />
		public async Task DeleteDirectory(string path, CancellationToken token)
		{
			path = ResolvePath(path);
			var di = new DirectoryInfo(path);
			if (!di.Exists)
				return;
			await NormalizeAndDelete(di, token);
		}

		/// <inheritdoc />
		public Task DeleteFile(string path, CancellationToken token)
		{
			return Task.Run(() => File.Delete(ResolvePath(path)), token);
		}

		/// <inheritdoc />
		public async Task<string> DownloadFile(string url, CancellationToken cancellationToken)
		{
			if (url == null)
				throw new ArgumentNullException(nameof(url));

			var request = WebRequest.Create(url);
			var tcs = new TaskCompletionSource<string>();

			using (cancellationToken.Register(() => request.Abort()))
			{
				request.BeginGetResponse(new AsyncCallback(async (r) =>
				{
					if (cancellationToken.IsCancellationRequested)
						tcs.SetCanceled();
					else
						using (var response = request.EndGetResponse(r))
						using (var reader = new StreamReader(response.GetResponseStream()))
							tcs.SetResult(await reader.ReadToEndAsync());
				}), null);

				return await tcs.Task;
			}
		}

		public Task<bool> FileExists(string path, CancellationToken token)
		{
			return Task.Run(() => File.Exists(ResolvePath(path)), token);
		}

		/// <inheritdoc />
		public string GetDirectoryName(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			return Path.GetDirectoryName(path);
		}

		/// <inheritdoc />
		public string GetFileName(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			return Path.GetFileName(path);
		}

		/// <inheritdoc />
		public string GetFileNameWithoutExtension(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			return Path.GetFileNameWithoutExtension(path);
		}

		/// <inheritdoc />
		public async Task<byte[]> ReadAllBytes(string path, CancellationToken token)
		{
			path = ResolvePath(path);
			using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, DefaultBufferSize, true))
			{
				byte[] buf;
#if LARGE_FILE_SUPPORT
				if (file.Length >= Int32.MaxValue)
				{
					var readsRequired = (file.Length / Int32.MaxValue) + (file.Length % Int32.MaxValue == 0 ? 0 : 1);
					var collection = new List<byte[]>();

					for (var I = 0; I < readsRequired; ++I)
					{
						token.ThrowIfCancellationRequested();
						var len = (int)(I == (readsRequired - 1) ? file.Length % Int32.MaxValue : Int32.MaxValue);
						buf = new byte[len];
						await file.ReadAsync(buf, 0, len, token);
						collection.Add(buf);
					}
					return collection.SelectMany(x => x).ToArray();
				}
#endif
				buf = new byte[file.Length];
				await file.ReadAsync(buf, 0, (int)file.Length, token);
				return buf;
			}
		}

		/// <inheritdoc />
		public virtual string ResolvePath(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			return Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
		}

		/// <inheritdoc />
		public async Task WriteAllBytes(string path, byte[] contents, CancellationToken token)
		{
			path = ResolvePath(path);
			using (var file = File.Open(path, FileMode.Create, FileAccess.Write))
				await file.WriteAsync(contents, 0, contents.Length, token);
		}
	}
}