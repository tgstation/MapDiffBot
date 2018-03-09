﻿using MapDiffBot.Core;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace MapDiffBot.Models
{
	/// <summary>
	/// Represents a database containing models
	/// </summary>
    public interface IDatabaseContext
	{
		/// <summary>
		/// The <see cref="UserAccessToken"/>s in the database
		/// </summary>
		DbSet<UserAccessToken> UserAccessTokens { get; set; }
		/// <summary>
		/// The <see cref="Installation"/>s in the database
		/// </summary>
		DbSet<Installation> Installations { get; set; }
		/// <summary>
		/// The <see cref="MapDiff"/>s in the database
		/// </summary>
		DbSet<MapDiff> MapDiffSets { get; set; }

		/// <summary>
		/// Save changes to the <see cref="IDatabaseContext"/>
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Save(CancellationToken cancellationToken);

		/// <summary>
		/// Ensure the <see cref="IDatabaseContext"/> is ready
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task Initialize(CancellationToken cancellationToken);

		/// <summary>
		/// Prevents the <see cref="IDatabaseContext"/> from being used by other threads for the duration of an operation
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="SemaphoreSlimContext"/> guarding the <see cref="IDatabaseContext"/></returns>
		Task<SemaphoreSlimContext> LockToCallStack(CancellationToken cancellationToken);
	}
}
