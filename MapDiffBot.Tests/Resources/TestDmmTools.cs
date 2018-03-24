using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MapDiffBot.Resources.Tests
{
	/// <summary>
	/// Tests for <see cref="DmmTools"/>
	/// </summary>
	[TestClass]
	public sealed class TestDmmTools
	{
		[TestMethod]
		public void TestType() => Assert.AreEqual(typeof(byte[]), DmmTools.dmm_tools.GetType());
	}
}
