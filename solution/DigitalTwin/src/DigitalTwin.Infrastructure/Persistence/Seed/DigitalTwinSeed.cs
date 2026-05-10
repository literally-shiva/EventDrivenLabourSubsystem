using DigitalTwin.Domain.Entities;

namespace DigitalTwin.Infrastructure.Persistence.Seed;

public static class DigitalTwinSeed
{
    public static async Task SeedAsync(DigitalTwinDbContext dbContext)
    {
        if (dbContext.Projects.Any())
        {
            return;
        }

        var projectId = Guid.NewGuid();
        var work1Id = Guid.NewGuid();
        var work2Id = Guid.NewGuid();
        var work3Id = Guid.NewGuid();
        var start = DateTime.UtcNow.Date;

        var project = new Project
        {
            Id = projectId,
            Name = "Demo BIM Design Project",
            StartDate = start,
            EndDate = start.AddDays(20),
            CurrentSimulationTime = start,
            Works =
            [
                new Work { Id = work1Id, ProjectId = projectId, Name = "Work1", StartDate = start, EndDate = start.AddDays(5), PlannedDuration = 5, CurrentDuration = 0 },
                new Work { Id = work2Id, ProjectId = projectId, Name = "Work2", StartDate = start.AddDays(5), EndDate = start.AddDays(11), PlannedDuration = 6, CurrentDuration = 0 },
                new Work { Id = work3Id, ProjectId = projectId, Name = "Work3", StartDate = start.AddDays(5), EndDate = start.AddDays(10), PlannedDuration = 5, CurrentDuration = 0 }
            ]
        };

        dbContext.Projects.Add(project);
        dbContext.WorkDependencies.AddRange(
            new WorkDependency { ParentWorkId = work1Id, ChildWorkId = work2Id },
            new WorkDependency { ParentWorkId = work1Id, ChildWorkId = work3Id });

        await dbContext.SaveChangesAsync();
    }
}
