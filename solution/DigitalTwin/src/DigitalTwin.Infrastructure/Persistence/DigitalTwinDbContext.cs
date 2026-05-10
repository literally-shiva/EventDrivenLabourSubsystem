using DigitalTwin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Persistence;

public class DigitalTwinDbContext(DbContextOptions<DigitalTwinDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Work> Works => Set<Work>();
    public DbSet<WorkDependency> WorkDependencies => Set<WorkDependency>();
    public DbSet<WorkMetricSnapshot> WorkMetricSnapshots => Set<WorkMetricSnapshot>();

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
            builder.HasMany(x => x.MetricSnapshots).WithOne(x => x.Work).HasForeignKey(x => x.WorkId);
        });

        modelBuilder.Entity<WorkDependency>(builder =>
        {
            builder.HasKey(x => new { x.ParentWorkId, x.ChildWorkId });
            builder.HasOne(x => x.ParentWork).WithMany(x => x.ParentDependencies).HasForeignKey(x => x.ParentWorkId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ChildWork).WithMany(x => x.ChildDependencies).HasForeignKey(x => x.ChildWorkId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkMetricSnapshot>(builder => { builder.HasKey(x => x.Id); });
    }
}
