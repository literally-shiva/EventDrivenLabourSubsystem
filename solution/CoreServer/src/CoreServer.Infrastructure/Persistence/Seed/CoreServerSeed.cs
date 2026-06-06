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
            // Initial labelled training set for the SVM classifier.
            // Feature vector order (matches DigitalTwin output):
            //   [workersCount, modelDataVolume, changesCount, collisionCount, approvalDelayDays, reworkCount]
            //
            // ModelDataVolume from DigitalTwin: 150 + progress*4.5 + rand*50
            //   ~350–450 at 50 % progress, ~500–600 at 80 % progress.
            //
            // Each class gets 3 vectors with varied magnitudes so that the SVM can
            // generalise, not just memorise two boundary points.
            //
            // Key separating features per class:
            //   ApprovalDelayed          — high approvalDelayDays (5–9), otherwise normal
            //   CollisionDetected        — high collisionCount (5–8), otherwise normal
            //   DesignRequirementChanged — high changesCount (6–9), low rework/delay
            //   DocumentationReturned    — moderate reworkCount (3–5), low approvalDelay (≤3),
            //                              no expert-review marker (no delay ≥5)
            //   ResourceShortage         — low workersCount (1–2), all other dims normal
            //   ExpertReviewFailed       — high reworkCount (4–6) AND high approvalDelayDays (4–7)
            //                              (distinguishes from DocumentationReturned)
            dbContext.EventPatterns.AddRange(
                // ── ApprovalDelayed ──────────────────────────────────────────────────────
                new EventPattern { Id = Guid.NewGuid(), Name = "Approval Delay A", Vector = JsonSerializer.Serialize(new[] { 4d, 420d, 1d, 0d, 7d, 1d }), EventType = EventType.ApprovalDelayed, EventTypeName = nameof(EventType.ApprovalDelayed), AverageDelayImpact = 1.18, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Approval Delay B", Vector = JsonSerializer.Serialize(new[] { 5d, 520d, 1d, 0d, 5d, 0d }), EventType = EventType.ApprovalDelayed, EventTypeName = nameof(EventType.ApprovalDelayed), AverageDelayImpact = 1.18, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Approval Delay C", Vector = JsonSerializer.Serialize(new[] { 6d, 380d, 2d, 0d, 9d, 1d }), EventType = EventType.ApprovalDelayed, EventTypeName = nameof(EventType.ApprovalDelayed), AverageDelayImpact = 1.18, CreatedAt = DateTime.UtcNow },

                // ── CollisionDetected ────────────────────────────────────────────────────
                new EventPattern { Id = Guid.NewGuid(), Name = "Collision Spike A", Vector = JsonSerializer.Serialize(new[] { 7d, 480d, 3d, 7d, 1d, 2d }), EventType = EventType.CollisionDetected, EventTypeName = nameof(EventType.CollisionDetected), AverageDelayImpact = 1.12, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Collision Spike B", Vector = JsonSerializer.Serialize(new[] { 8d, 590d, 2d, 6d, 0d, 1d }), EventType = EventType.CollisionDetected, EventTypeName = nameof(EventType.CollisionDetected), AverageDelayImpact = 1.12, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Collision Spike C", Vector = JsonSerializer.Serialize(new[] { 5d, 430d, 1d, 8d, 1d, 2d }), EventType = EventType.CollisionDetected, EventTypeName = nameof(EventType.CollisionDetected), AverageDelayImpact = 1.12, CreatedAt = DateTime.UtcNow },

                // ── DesignRequirementChanged ─────────────────────────────────────────────
                new EventPattern { Id = Guid.NewGuid(), Name = "Design Change A", Vector = JsonSerializer.Serialize(new[] { 5d, 460d, 8d, 0d, 0d, 0d }), EventType = EventType.DesignRequirementChanged, EventTypeName = nameof(EventType.DesignRequirementChanged), AverageDelayImpact = 1.10, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Design Change B", Vector = JsonSerializer.Serialize(new[] { 6d, 540d, 7d, 1d, 0d, 0d }), EventType = EventType.DesignRequirementChanged, EventTypeName = nameof(EventType.DesignRequirementChanged), AverageDelayImpact = 1.10, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Design Change C", Vector = JsonSerializer.Serialize(new[] { 4d, 390d, 9d, 0d, 1d, 1d }), EventType = EventType.DesignRequirementChanged, EventTypeName = nameof(EventType.DesignRequirementChanged), AverageDelayImpact = 1.10, CreatedAt = DateTime.UtcNow },

                // ── DocumentationReturned ────────────────────────────────────────────────
                // Signature: moderate rework (3–5), low approvalDelay (≤3), no expert-delay spike
                new EventPattern { Id = Guid.NewGuid(), Name = "Documentation Return A", Vector = JsonSerializer.Serialize(new[] { 6d, 500d, 2d, 0d, 2d, 4d }), EventType = EventType.DocumentationReturned, EventTypeName = nameof(EventType.DocumentationReturned), AverageDelayImpact = 1.15, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Documentation Return B", Vector = JsonSerializer.Serialize(new[] { 5d, 580d, 1d, 0d, 1d, 5d }), EventType = EventType.DocumentationReturned, EventTypeName = nameof(EventType.DocumentationReturned), AverageDelayImpact = 1.15, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Documentation Return C", Vector = JsonSerializer.Serialize(new[] { 7d, 440d, 3d, 0d, 3d, 3d }), EventType = EventType.DocumentationReturned, EventTypeName = nameof(EventType.DocumentationReturned), AverageDelayImpact = 1.15, CreatedAt = DateTime.UtcNow },

                // ── ResourceShortage ─────────────────────────────────────────────────────
                new EventPattern { Id = Guid.NewGuid(), Name = "Resource Shortage A", Vector = JsonSerializer.Serialize(new[] { 1d, 390d, 0d, 0d, 0d, 0d }), EventType = EventType.ResourceShortage, EventTypeName = nameof(EventType.ResourceShortage), AverageDelayImpact = 1.14, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Resource Shortage B", Vector = JsonSerializer.Serialize(new[] { 2d, 470d, 1d, 0d, 0d, 0d }), EventType = EventType.ResourceShortage, EventTypeName = nameof(EventType.ResourceShortage), AverageDelayImpact = 1.14, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Resource Shortage C", Vector = JsonSerializer.Serialize(new[] { 1d, 550d, 0d, 0d, 1d, 1d }), EventType = EventType.ResourceShortage, EventTypeName = nameof(EventType.ResourceShortage), AverageDelayImpact = 1.14, CreatedAt = DateTime.UtcNow },

                // ── ExpertReviewFailed ───────────────────────────────────────────────────
                // Signature: high rework (4–6) AND high approvalDelay (4–7) together.
                // This combo separates it from DocumentationReturned (low delay) and
                // ApprovalDelayed (low rework).
                new EventPattern { Id = Guid.NewGuid(), Name = "Expert Review Fail A", Vector = JsonSerializer.Serialize(new[] { 6d, 510d, 1d, 0d, 5d, 5d }), EventType = EventType.ExpertReviewFailed, EventTypeName = nameof(EventType.ExpertReviewFailed), AverageDelayImpact = 1.20, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Expert Review Fail B", Vector = JsonSerializer.Serialize(new[] { 7d, 600d, 2d, 0d, 4d, 6d }), EventType = EventType.ExpertReviewFailed, EventTypeName = nameof(EventType.ExpertReviewFailed), AverageDelayImpact = 1.20, CreatedAt = DateTime.UtcNow },
                new EventPattern { Id = Guid.NewGuid(), Name = "Expert Review Fail C", Vector = JsonSerializer.Serialize(new[] { 5d, 455d, 1d, 0d, 6d, 4d }), EventType = EventType.ExpertReviewFailed, EventTypeName = nameof(EventType.ExpertReviewFailed), AverageDelayImpact = 1.20, CreatedAt = DateTime.UtcNow });
        }

        await dbContext.SaveChangesAsync();
    }
}
