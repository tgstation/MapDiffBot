using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core
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
			await Task.WhenAll(tasks).ConfigureAwait(false);
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
				await destStream.WriteAsync(buf, 0, buf.Length, token).ConfigureAwait(false);
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
				await srcStream.CopyToAsync(destStream, DefaultBufferSize, token).ConfigureAwait(false);
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
			await NormalizeAndDelete(di, token).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public Task DeleteFile(string path, CancellationToken token)
		{
			return Task.Run(() => File.Delete(ResolvePath(path)), token);
		}

		public Task<bool> FileExists(string path, CancellationToken token)
		{
			return Task.Run(() => File.Exists(ResolvePath(path)), token);
		}

		/// <inheritdoc />
		public string GetFileName(string path) => Path.GetFileName(path ?? throw new ArgumentNullException(nameof(path)));

		/// <inheritdoc />
		public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path ?? throw new ArgumentNullException(nameof(path)));

		/// <inheritdoc />
		public Task<List<string>> GetFilesWithExtension(string path, string extension, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			path = ResolvePath(path);
			if (extension == null)
				throw new ArgumentNullException(extension);
			var results = new List<string>();
			foreach (var I in Directory.EnumerateFiles(path, String.Format(CultureInfo.InvariantCulture, "*.{0}", extension), SearchOption.TopDirectoryOnly))
			{
				cancellationToken.ThrowIfCancellationRequested();
				results.Add(I);
			}
			return results;
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task MoveFile(string source, string destination, CancellationToken cancellationToken) => Task.Factory.StartNew(() => File.Move(source, destination), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public async Task<byte[]> ReadAllBytes(string path, CancellationToken token)
		{
			path = ResolvePath(path);
			using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, DefaultBufferSize, true))
			{
				byte[] buf;
				buf = new byte[file.Length];
				await file.ReadAsync(buf, 0, (int)file.Length, token).ConfigureAwait(false);
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
				await file.WriteAsync(contents, 0, contents.Length, token).ConfigureAwait(false);
		}
	}
}