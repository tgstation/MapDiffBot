using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Octokit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Core.Tests
{
	[TestClass]
	public sealed class TestLocalRepositoryManager
	{
		[TestMethod]
		public void TestInstantiation()
		{
			Assert.ThrowsException<ArgumentNullException>(() => new LocalRepositoryManager(null, null, null));
			var mockIOManager = new Mock<IIOManager>();
			Assert.ThrowsException<ArgumentNullException>(() => new LocalRepositoryManager(mockIOManager.Object, null, null));
			var mockLocalRepositoryFactory = new Mock<ILocalRepositoryFactory>();
			Assert.ThrowsException<ArgumentNullException>(() => new LocalRepositoryManager(mockIOManager.Object, mockLocalRepositoryFactory.Object, null));
			var mockRepositoryOperations = new Mock<IRepositoryOperations>();
			mockIOManager.Setup(x => x.ResolvePath(".")).Returns(".");
			var lrm = new LocalRepositoryManager(mockIOManager.Object, mockLocalRepositoryFactory.Object, mockRepositoryOperations.Object);
		}

		[TestMethod]
		public async Task TestMultiGet()
		{
			var mockIOManager = new Mock<IIOManager>();
			mockIOManager.Setup(x => x.ResolvePath(".")).Returns(".");
			var mockLocalRepositoryFactory = new Mock<ILocalRepositoryFactory>();
			var mockRepositoryOperations = new Mock<IRepositoryOperations>();
			var lrm = new LocalRepositoryManager(mockIOManager.Object, mockLocalRepositoryFactory.Object, mockRepositoryOperations.Object);

			const string FakeUrl = "https://github.com/tgstation/tgstation";
			const string Identifier = "tgstation\\tgstation";
			var repoModel = new Repository(null, null, FakeUrl, null, null, null, null, 1, new User(null, null, null, 0, null, DateTimeOffset.Now, DateTimeOffset.Now, 0, null, 0, 0, null, null, 0, 0, null, "tgstation", null, 0, null, 0, 0, 0, null, null, false, null, null), "tgstation", null, null, null, null, false, false, 0, 0, null, 0, null, DateTimeOffset.Now, DateTimeOffset.Now, null, null, null, null, false, false, false, false, 0, 0, null, null, null);
			var mockLocalRepository = new Mock<ILocalRepository>();
			TaskCompletionSource<object> firstTcs = null, continueTcs = new TaskCompletionSource<object>();
			mockLocalRepositoryFactory.Setup(x => x.CreateLocalRepository(Identifier, It.IsNotNull<TaskCompletionSource<object>>(), default)).Callback((string id, TaskCompletionSource<object> tcs, CancellationToken cancellationToken) =>
			{
				firstTcs = tcs;
			}).Returns(Task.FromResult(mockLocalRepository.Object)).Verifiable();

			var firstBlocked = false;
			async Task FirstGet()
			{
				await lrm.GetRepository(repoModel, (progress) => Task.CompletedTask, () => { firstBlocked = true; return Task.CompletedTask; }, default).ConfigureAwait(false);
				await continueTcs.Task.ConfigureAwait(false);
				firstTcs.SetResult(null);
			};

			var blocked = false;
			async Task SecondGet()
			{
				await lrm.GetRepository(repoModel, (progress) => Task.CompletedTask, () => { blocked = true; return Task.CompletedTask; }, default).ConfigureAwait(false);
			};

			var firstGet = FirstGet();
			await Task.Delay(100).ConfigureAwait(false);
			var secondGet = SecondGet();
			await Task.Delay(100).ConfigureAwait(false);
			continueTcs.SetResult(null);
			await firstGet.ConfigureAwait(false);
			await secondGet.ConfigureAwait(false);

			mockLocalRepository.VerifyAll();
			Assert.IsTrue(blocked);
			Assert.IsFalse(firstBlocked);
		}
	}
}
