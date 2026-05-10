using CoreServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreServer.Infrastructure.Persistence;

public class CoreServerDbContext(DbContextOptions<CoreServerDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Work> Works => Set<Work>();
    public DbSet<MetricHistory> MetricsHistory => Set<MetricHistory>();
    public DbSet<DetectedEvent> DetectedEvents => Set<DetectedEvent>();
    public DbSet<EventPattern> EventPatterns => Set<EventPattern>();
    public DbSet<WorkMarkovState> WorkMarkovStates => Set<WorkMarkovState>();
    public DbSet<DurationHistory> DurationHistory => Set<DurationHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.HasMany(x => x.Works).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId);
        });

        modelBuilder.Entity<Work>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<MetricHistory>(builder => builder.HasKey(x => x.Id));
        modelBuilder.Entity<DetectedEvent>(builder => builder.HasKey(x => x.Id));
        modelBuilder.Entity<EventPattern>(builder => builder.HasKey(x => x.Id));
        modelBuilder.Entity<WorkMarkovState>(builder => builder.HasKey(x => x.WorkId));
        modelBuilder.Entity<DurationHistory>(builder => builder.HasKey(x => x.Id));
    }
}
