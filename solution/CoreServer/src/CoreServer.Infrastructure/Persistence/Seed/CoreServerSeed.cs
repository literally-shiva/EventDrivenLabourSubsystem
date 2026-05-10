using System.Text.Json;
using CoreServer.Domain.Entities;
using CoreServer.Domain.Enums;

namespace CoreServer.Infrastructure.Persistence.Seed;

public static class CoreServerSeed
{
    public static async Task SeedAsync(CoreServerDbContext dbContext)
    {
        if (!dbContext.EventPatterns.Any())
        {
            dbContext.EventPatterns.AddRange(
                new EventPattern
                {
                    Id = Guid.NewGuid(),
                    Name = "Approval Delay Pattern",
                    Vector = JsonSerializer.Serialize(new[] { 4d, 260d, 1d, 0d, 6d, 1d }),
                    EventType = EventType.ApprovalDelayed,
                    AverageDelayImpact = 1.18,
                    CreatedAt = DateTime.UtcNow
                },
                new EventPattern
                {
                    Id = Guid.NewGuid(),
                    Name = "Collision Spike Pattern",
                    Vector = JsonSerializer.Serialize(new[] { 7d, 320d, 3d, 7d, 1d, 2d }),
                    EventType = EventType.CollisionDetected,
                    AverageDelayImpact = 1.12,
                    CreatedAt = DateTime.UtcNow
                });
        }

        if (!dbContext.Projects.Any())
        {
            var projectId = Guid.NewGuid();
            var start = DateTime.UtcNow.Date;
            dbContext.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Demo BIM Design Project",
                StartDate = start,
                EndDate = start.AddDays(20),
                Works =
                [
                    new Work { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Work1", StartDate = start, EndDate = start.AddDays(5), PlannedDuration = 5, CurrentDuration = 5 },
                    new Work { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Work2", StartDate = start.AddDays(5), EndDate = start.AddDays(11), PlannedDuration = 6, CurrentDuration = 6 },
                    new Work { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Work3", StartDate = start.AddDays(5), EndDate = start.AddDays(10), PlannedDuration = 5, CurrentDuration = 5 }
                ]
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
