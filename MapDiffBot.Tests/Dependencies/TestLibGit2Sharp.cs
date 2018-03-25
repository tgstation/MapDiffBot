using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MapDiffBot.Tests.Dependencies
{
	/// <summary>
	/// Tests for <see cref="LibGit2Sharp"/>
	/// </summary>
	[TestClass]
	public sealed class TestLibGit2Sharp
	{
		[TestMethod]
		public void TestNativeDllLoadsCorrectly()
		{
			var repo = new Repository();
		}
	}
}