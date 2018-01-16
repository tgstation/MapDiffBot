using System;
using System.Globalization;

namespace MapDiffBot.Generator
{
	/// <inheritdoc />
	sealed class MapDiff : IMapDiff
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
		/// <param name="_originalMapName">The value of <see cref="OriginalMapName"/></param>
		/// <param name="_beforePath">The value of <see cref="BeforePath"/></param>
		/// <param name="_afterPath">The valuse of <see cref="AfterPath"/></param>
		public MapDiff(string _originalMapName, string _beforePath, string _afterPath)
		{
			originalMapName = _originalMapName ?? throw new ArgumentNullException(nameof(_originalMapName));
;			if (_beforePath == null && _afterPath == null)
				throw new ArgumentNullException(nameof(_afterPath), String.Format(CultureInfo.CurrentCulture, "At least one of {0} or {1} must not be null!", nameof(_beforePath), nameof(_afterPath)));
			beforePath = _beforePath;
			afterPath = _afterPath;
		}
	}
}
