using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// Interface for using filesystems
	/// </summary>
	interface IIOManager
	{
		/// <summary>
		/// Retrieve the full path of some <paramref name="path"/> given a relative path. Must be used before passing relative paths to other APIs. All other operations in this <see langword="interface"/> call this internally on given paths
		/// </summary>
		/// <param name="path">Some path to retrieve the full path of</param>
		/// <returns><paramref name="path"/> as a full canonical path</returns>
		string ResolvePath(string path);

		/// <summary>
		/// Gets the directory name portion of a <paramref name="path"/>
		/// </summary>
		/// <param name="path">The path to get the file name of</param>
		/// <returns>The directory name portion of <paramref name="path"/></returns>
		string GetDirectoryName(string path);

		/// <summary>
		/// Gets the file name portion of a <paramref name="path"/>
		/// </summary>
		/// <param name="path">The path to get the file name of</param>
		/// <returns>The file name portion of <paramref name="path"/></returns>
		string GetFileName(string path);

		/// <summary>
		/// Check that the file at <paramref name="path"/> exists
		/// </summary>
		/// <param name="path">The file to check for existence</param>
		/// <param name="token">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> resulting in <see langword="true"/> if the file at <paramref name="path"/> exists, <see langword="false"/> otherwise</returns>
		Task<bool> FileExists(string path, CancellationToken token);

		/// <summary>
		/// Writes some <paramref name="additional_contents"/> to a file at <paramref name="path"/> after previous content
		/// </summary>
		/// <param name="path">The path of the file to write</param>
		/// <param name="additional_contents">The contents to add to the file</param>
		/// <param name="token">A <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task AppendAllText(string path, string additional_contents, CancellationToken token);

		/// <summary>
		/// Returns all the contents of a file at <paramref name="path"/> as a <see cref="byte"/> array
		/// </summary>
		/// <param name="path">The path of the file to read</param>
		/// <param name="token">A <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> that results in the contents of a file at <paramref name="path"/></returns>
		Task<byte[]> ReadAllBytes(string path, CancellationToken token);

		/// <summary>
		/// Writes some <paramref name="contents"/> to a file at <paramref name="path"/> overwriting previous content
		/// </summary>
		/// <param name="path">The path of the file to write</param>
		/// <param name="contents">The contents of the file</param>
		/// <param name="token">A <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task WriteAllBytes(string path, byte[] contents, CancellationToken token);

		/// <summary>
		/// Deletes a file at <paramref name="path"/>
		/// </summary>
		/// <param name="path">The path of the file to delete</param>
		/// <param name="token">A <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task DeleteFile(string path, CancellationToken token);

		/// <summary>
		/// Copy a file from <paramref name="src"/> to <paramref name="dest"/>
		/// </summary>
		/// <param name="src">The source file to copy</param>
		/// <param name="dest">The destination path</param>
		/// <param name="token">A <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task CopyFile(string src, string dest, CancellationToken token);

		/// <summary>
		/// Create a directory at <paramref name="path"/>
		/// </summary>
		/// <param name="path">The path of the directory to create</param>
		/// <param name="token">A <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task CreateDirectory(string path, CancellationToken token);

		/// <summary>
		/// Recursively delete a directory
		/// </summary>
		/// <param name="path">The path to the directory to delete</param>
		/// <param name="token">A <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task DeleteDirectory(string path, CancellationToken token);

		/// <summary>
		/// Downloads a file from <paramref name="url"/>
		/// </summary>
		/// <param name="url">The URL to download</param>
		/// <param name="token">A <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the operation</returns>
		Task<string> DownloadFile(string url, CancellationToken token);

		/// <summary>
		/// Combines an array of strings into a path
		/// </summary>
		/// <param name="paths">The paths to combine</param>
		/// <returns>The combined path</returns>
		string ConcatPath(params string[] paths);
	}
}
