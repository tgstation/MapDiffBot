using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Octokit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core.Tests
{
	/// <summary>
	/// Tests for <see cref="LocalRepositoryManager"/>
	/// </summary>
	[TestClass]
	public sealed class TestLocalRepositoryManager
	{
		[TestMethod]
		public void TestInstantiation()
		{
			Assert.ThrowsException<ArgumentNullException>(() => new LocalRepositoryManager(null, null, null, null));
			var mockIOManager = new Mock<IIOManager>();
			Assert.ThrowsException<ArgumentNullException>(() => new LocalRepositoryManager(mockIOManager.Object, null, null, null));
			var mockLocalRepositoryFactory = new Mock<ILocalRepositoryFactory>();
			Assert.ThrowsException<ArgumentNullException>(() => new LocalRepositoryManager(mockIOManager.Object, mockLocalRepositoryFactory.Object, null, null));
			var mockRepositoryOperations = new Mock<IRepositoryOperations>();
			mockIOManager.Setup(x => x.ResolvePath(".")).Returns(".");
			Assert.ThrowsException<ArgumentNullException>(() => new LocalRepositoryManager(mockIOManager.Object, mockLocalRepositoryFactory.Object, mockRepositoryOperations.Object, null));
			var mockLogger = new Mock<ILogger<LocalRepositoryManager>>();
			var lrm = new LocalRepositoryManager(mockIOManager.Object, mockLocalRepositoryFactory.Object, mockRepositoryOperations.Object, mockLogger.Object);
		}
	}
}
