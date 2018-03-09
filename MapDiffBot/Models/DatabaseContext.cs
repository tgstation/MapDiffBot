using MapDiffBot.Core;
using MapDiffBot.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using ZNetCS.AspNetCore.Logging.EntityFrameworkCore;

namespace MapDiffBot.Models
{
	/// <inheritdoc />
#pragma warning disable CA1812
	sealed class DatabaseContext : DbContext, IDatabaseContext
#pragma warning restore CA1812
	{
		/// <inheritdoc />
		public DbSet<UserAccessToken> UserAccessTokens
		{
			get
			{
				CheckSemaphoreLocked();
				return userAccessTokens;
			}
			set => userAccessTokens = value;
		}

		/// <inheritdoc />
		public DbSet<Installation> Installations
		{
			get
			{
				CheckSemaphoreLocked();
				return installations;
			}
			set => installations = value;
		}

		/// <inheritdoc />
		public DbSet<InstallationRepository> InstallationRepositories
		{
			get
			{
				CheckSemaphoreLocked();
				return installationRepositories;
			}
			set => installationRepositories = value;
		}

		/// <inheritdoc />
		public DbSet<MapDiff> MapDiffs
		{
			get
			{
				CheckSemaphoreLocked();
				return mapDiffSets;
			}
			set => mapDiffSets = value;
		}

		/// <summary>
		/// The <see cref="DbSet{TEntity}"/> for <see cref="Log"/>s
		/// </summary>
		public DbSet<Log> Logs { get; set; }

		/// <summary>
		/// The <see cref="DbSet{TEntity}"/> for <see cref="Image"/>s
		/// </summary>
		public DbSet<Image> Images { get; set; }

		/// <summary>
		/// The <see cref="DatabaseConfiguration"/> for the <see cref="DatabaseContext"/>
		/// </summary>
		readonly DatabaseConfiguration databaseConfiguration;
		/// <summary>
		/// The <see cref="ILoggerFactory"/> for the <see cref="DatabaseContext"/>
		/// </summary>
		readonly ILoggerFactory loggerFactory;
		/// <summary>
		/// The <see cref="SemaphoreSlim"/> for the <see cref="DatabaseContext"/>
		/// </summary>
		readonly SemaphoreSlim semaphore;

		/// <summary>
		/// Backing field for <see cref="UserAccessTokens"/>
		/// </summary>
		DbSet<UserAccessToken> userAccessTokens;
		/// <summary>
		/// Backing field for <see cref="Installations"/>
		/// </summary>
		DbSet<Installation> installations;
		/// <summary>
		/// Backing field for <see cref="InstallationRepositories"/>
		/// </summary>
		DbSet<InstallationRepository> installationRepositories;
		/// <summary>
		/// Backing field for <see cref="MapDiffs"/>
		/// </summary>
		DbSet<MapDiff> mapDiffSets;

		/// <inheritdoc />
		public override void Dispose()
		{
			base.Dispose();
			semaphore.Dispose();
		}

		/// <summary>
		/// Checks tha <see cref="semaphore"/> is locked
		/// </summary>
		void CheckSemaphoreLocked()
		{
			if (semaphore.CurrentCount > 0)
				throw new InvalidOperationException("The DatabaseContext is not locked!");
		}

		/// <summary>
		/// Construct a <see cref="DatabaseContext"/>
		/// </summary>
		/// <param name="options">The <see cref="DbContextOptions{TContext}"/> for the <see cref="DatabaseContext"/></param>
		/// <param name="databaseConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing the value of <see cref="databaseConfiguration"/></param>
		/// <param name="loggerFactory">The value of <see cref="loggerFactory"/></param>
		public DatabaseContext(DbContextOptions<DatabaseContext> options, IOptions<DatabaseConfiguration> databaseConfigurationOptions, ILoggerFactory loggerFactory) : base(options)
		{
			databaseConfiguration = databaseConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(databaseConfigurationOptions));
			this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
			semaphore = new SemaphoreSlim(1);
		}

		/// <inheritdoc />
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// build default model.
			LogModelBuilderHelper.Build(modelBuilder.Entity<Log>());
			// real relation database can map table:
			modelBuilder.Entity<Log>().ToTable(nameof(Log));

			modelBuilder.Entity<MapDiff>().HasKey(x => new { x.RepositoryId, x.PullRequestNumber, x.FileId });
		}

		/// <inheritdoc />
		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if (databaseConfiguration.IsMySQL)
				optionsBuilder.UseMySQL(databaseConfiguration.ConnectionString);
			else
				optionsBuilder.UseSqlServer(databaseConfiguration.ConnectionString);
			optionsBuilder.UseLoggerFactory(loggerFactory);
		}

		/// <inheritdoc />
		public Task Save(CancellationToken cancellationToken)
		{
			CheckSemaphoreLocked();
			return SaveChangesAsync(cancellationToken);
		}

		/// <inheritdoc />
		public Task Initialize(CancellationToken cancellationToken) => Database.EnsureCreatedAsync(cancellationToken);


		/// <inheritdoc />
		public Task<SemaphoreSlimContext> LockToCallStack(CancellationToken cancellationToken) => SemaphoreSlimContext.Lock(semaphore, cancellationToken);
	}
}
