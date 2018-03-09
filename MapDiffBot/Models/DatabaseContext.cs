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
		public DbSet<UserAccessToken> UserAccessTokens { get; set; }

		/// <inheritdoc />
		public DbSet<Installation> Installations { get; set; }

		/// <inheritdoc />
		public DbSet<InstallationRepository> InstallationRepositories { get; set; }

		/// <inheritdoc />
		public DbSet<MapDiff> MapDiffs { get; set; }

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
		/// Construct a <see cref="DatabaseContext"/>
		/// </summary>
		/// <param name="options">The <see cref="DbContextOptions{TContext}"/> for the <see cref="DatabaseContext"/></param>
		/// <param name="databaseConfigurationOptions">The <see cref="IOptions{TOptions}"/> containing the value of <see cref="databaseConfiguration"/></param>
		/// <param name="loggerFactory">The value of <see cref="loggerFactory"/></param>
		public DatabaseContext(DbContextOptions<DatabaseContext> options, IOptions<DatabaseConfiguration> databaseConfigurationOptions, ILoggerFactory loggerFactory) : base(options)
		{
			databaseConfiguration = databaseConfigurationOptions?.Value ?? throw new ArgumentNullException(nameof(databaseConfigurationOptions));
			this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
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
			modelBuilder.Entity<InstallationRepository>().HasKey(x => new { x.ColumnId, x.Id });
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
		public Task Save(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

		/// <inheritdoc />
		public Task Initialize(CancellationToken cancellationToken) => Database.EnsureCreatedAsync(cancellationToken);
	}
}
