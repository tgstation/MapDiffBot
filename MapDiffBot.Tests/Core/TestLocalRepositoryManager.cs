using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Octokit;
using System;
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

			//async set results because tcs continuation memes
			Task SetResult(TaskCompletionSource<object> taskCompletionSource) => Task.Run(() => taskCompletionSource.SetResult(null));

			const string FakeUrl = "https://github.com/tgstation/tgstation";
			const string Identifier = "tgstation\\tgstation";
			var repoModel = new Repository(null, null, FakeUrl, null, null, null, null, 1, new User(null, null, null, 0, null, DateTimeOffset.Now, DateTimeOffset.Now, 0, null, 0, 0, null, null, 0, 0, null, "tgstation", null, 0, null, 0, 0, 0, null, null, false, null, null), "tgstation", null, null, null, null, false, false, 0, 0, null, 0, null, DateTimeOffset.Now, DateTimeOffset.Now, null, null, null, null, false, false, false, false, 0, 0, null, null, null);
			var mockLocalRepository = new Mock<ILocalRepository>();
			TaskCompletionSource<object> firstTcs = null, continueTcs = new TaskCompletionSource<object>(), ensuranceTcs = new TaskCompletionSource<object>();
			var mockLocalObject = mockLocalRepository.Object;
			mockLocalRepositoryFactory.Setup(x => x.CreateLocalRepository(Identifier, It.IsNotNull<TaskCompletionSource<object>>(), default)).Callback((string id, TaskCompletionSource<object> tcs, CancellationToken cancellationToken) =>
			{
				if (firstTcs != null)
					return;
				firstTcs = tcs;
				SetResult(ensuranceTcs);
			}).Returns(Task.FromResult(mockLocalObject)).Verifiable();

			var firstBlocked = false;
			async Task FirstGet()
			{
				Assert.AreSame(mockLocalObject, await lrm.GetRepository(repoModel, (progress) => Task.CompletedTask, () => { firstBlocked = true; return Task.CompletedTask; }, default).ConfigureAwait(false));
				await continueTcs.Task.ConfigureAwait(false);
				await ensuranceTcs.Task.ConfigureAwait(false);
				var t = SetResult(firstTcs);
			};

			var blocked = false;
			async Task SecondGet()
			{
				Assert.AreSame(mockLocalObject, await lrm.GetRepository(repoModel, (progress) => Task.CompletedTask, () => { blocked = true; return Task.CompletedTask; }, default).ConfigureAwait(false));
			};

			var firstGet = FirstGet();
			var secondGet = SecondGet();
			var t2 = SetResult(continueTcs);
			await firstGet.ConfigureAwait(false);
			await secondGet.ConfigureAwait(false);

			mockLocalRepository.VerifyAll();
			Assert.IsTrue(blocked);
			Assert.IsFalse(firstBlocked);
		}
	}
}
