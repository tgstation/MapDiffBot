using System;
using System.Globalization;

namespace MapDiffBot.Generator
{
	/// <inheritdoc />
	public sealed class MapDiff : IMapDiff
	{
		/// <inheritdoc />
		public string OriginalMapName => originalMapName;
		/// <inheritdoc />
		public string BeforePath => beforePath;
		/// <inheritdoc />
		public string AfterPath => afterPath;

		/// <summary>
		/// Backing field for <see cref="OriginalMapName"/>
		/// </summary>
		readonly string originalMapName;
		/// <summary>
		/// Backing field for <see cref="BeforePath"/>
		/// </summary>
		readonly string beforePath;
		/// <summary>
		/// Backing field for <see cref="AfterPath"/>
		/// </summary>
		readonly string afterPath;

		/// <summary>
		/// Construct a <see cref="MapDiff"/>
		/// </summary>
		/// <param name="originalMapName">The value of <see cref="OriginalMapName"/></param>
		/// <param name="beforePath">The value of <see cref="BeforePath"/></param>
		/// <param name="afterPath">The valuse of <see cref="AfterPath"/></param>
		public MapDiff(string originalMapName, string beforePath, string afterPath)
		{
			this.originalMapName = originalMapName ?? throw new ArgumentNullException(nameof(originalMapName));
			this.beforePath = beforePath;
			this.afterPath = afterPath;
		}
	}
}
