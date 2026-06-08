using System.Text.Json;
using CoreServer.Domain.Entities;
using CoreServer.Domain.Enums;

namespace CoreServer.Infrastructure.Persistence.Seed;

public static class SyntheticTrainingDataGenerator
{
    private static readonly Random Random = new();

    /// <summary>
    /// Генерирует синтетические обучающие векторы для каждого типа события.
    /// Векторы соответствуют тому, что генерирует симуляция DigitalTwin.
    /// </summary>
    public static List<EventPattern> GenerateTrainingData(int samplesPerClass = 25)
    {
        var patterns = new List<EventPattern>();

        // Генерируем по samplesPerClass векторов для каждого типа события
        patterns.AddRange(GenerateCollisionDetected(samplesPerClass));
        patterns.AddRange(GenerateDesignRequirementChanged(samplesPerClass));
        patterns.AddRange(GenerateApprovalDelayed(samplesPerClass));
        patterns.AddRange(GenerateDocumentationReturned(samplesPerClass));
        patterns.AddRange(GenerateExpertReviewFailed(samplesPerClass));
        patterns.AddRange(GenerateResourceShortage(samplesPerClass));

        return patterns;
    }

    /// <summary>
    /// CollisionDetected: высокий collisionCount (4-15)
    /// Симуляция: BaseCollisions += Random(4, 9)
    /// </summary>
    private static List<EventPattern> GenerateCollisionDetected(int count)
    {
        var patterns = new List<EventPattern>();
        for (int i = 0; i < count; i++)
        {
            // Базовые значения (нормальное состояние)
            var workers = Random.Next(4, 9);
            var modelVolume = Random.Next(350, 601);
            var changes = Random.Next(0, 4);
            var baseCollisions = Random.Next(0, 4);
            var approvalDelay = Random.Next(0, 3);
            var baseRework = Random.Next(0, 3);

            // Событие: скачок коллизий +4-8
            var collisions = baseCollisions + Random.Next(4, 9);
            var rework = baseRework + Random.Next(1, 3);

            var vector = new[] { (double)workers, (double)modelVolume, (double)changes, (double)collisions, (double)approvalDelay, (double)rework };

            patterns.Add(new EventPattern
            {
                Id = Guid.NewGuid(),
                Name = $"Collision Detected {i + 1}",
                Vector = JsonSerializer.Serialize(vector),
                EventType = EventType.CollisionDetected,
                EventTypeName = nameof(EventType.CollisionDetected),
                AverageDelayImpact = 1.12,
                CreatedAt = DateTime.UtcNow
            });
        }
        return patterns;
    }

    /// <summary>
    /// DesignRequirementChanged: высокий changesCount (3-12), повышенный rework (2-6)
    /// Симуляция: BaseChanges += Random(3, 7), BaseRework += Random(2, 5), BaseCollisions += Random(0, 3)
    /// </summary>
    private static List<EventPattern> GenerateDesignRequirementChanged(int count)
    {
        var patterns = new List<EventPattern>();
        for (int i = 0; i < count; i++)
        {
            var workers = Random.Next(4, 9);
            var modelVolume = Random.Next(350, 601);
            var baseChanges = Random.Next(0, 4);
            var baseCollisions = Random.Next(0, 3);
            var approvalDelay = Random.Next(0, 2);
            var baseRework = Random.Next(0, 2);

            // Событие: скачок изменений +3-6
            var changes = baseChanges + Random.Next(3, 7);
            var rework = baseRework + Random.Next(2, 5);
            var collisions = baseCollisions + Random.Next(0, 3);

            var vector = new[] { (double)workers, (double)modelVolume, (double)changes, (double)collisions, (double)approvalDelay, (double)rework };

            patterns.Add(new EventPattern
            {
                Id = Guid.NewGuid(),
                Name = $"Design Requirement Changed {i + 1}",
                Vector = JsonSerializer.Serialize(vector),
                EventType = EventType.DesignRequirementChanged,
                EventTypeName = nameof(EventType.DesignRequirementChanged),
                AverageDelayImpact = 1.10,
                CreatedAt = DateTime.UtcNow
            });
        }
        return patterns;
    }

    /// <summary>
    /// ApprovalDelayed: высокий approvalDelayDays (5-12)
    /// Симуляция: GenerateApprovalDelay returns Random(5, 13)
    /// </summary>
    private static List<EventPattern> GenerateApprovalDelayed(int count)
    {
        var patterns = new List<EventPattern>();
        for (int i = 0; i < count; i++)
        {
            var workers = Random.Next(4, 9);
            var modelVolume = Random.Next(350, 601);
            var changes = Random.Next(0, 4);
            var collisions = Random.Next(0, 4);
            var baseApprovals = Random.Next(0, 3);
            var rework = Random.Next(0, 3);

            // Событие: значительная задержка 5-12 дней
            var approvalDelay = Random.Next(5, 13);

            var vector = new[] { (double)workers, (double)modelVolume, (double)changes, (double)collisions, (double)approvalDelay, (double)rework };

            patterns.Add(new EventPattern
            {
                Id = Guid.NewGuid(),
                Name = $"Approval Delayed {i + 1}",
                Vector = JsonSerializer.Serialize(vector),
                EventType = EventType.ApprovalDelayed,
                EventTypeName = nameof(EventType.ApprovalDelayed),
                AverageDelayImpact = 1.18,
                CreatedAt = DateTime.UtcNow
            });
        }
        return patterns;
    }

    /// <summary>
    /// DocumentationReturned: умеренный rework (2-6), низкая задержка (2-4)
    /// Симуляция: BaseRework += Random(2, 5), TemporaryApprovalDelay = Random(2, 5)
    /// </summary>
    private static List<EventPattern> GenerateDocumentationReturned(int count)
    {
        var patterns = new List<EventPattern>();
        for (int i = 0; i < count; i++)
        {
            var workers = Random.Next(4, 9);
            var modelVolume = Random.Next(350, 601);
            var changes = Random.Next(0, 3);
            var collisions = Random.Next(0, 3);
            var baseRework = Random.Next(0, 3);

            // Событие: возврат документации
            var rework = baseRework + Random.Next(2, 5);
            var approvalDelay = Random.Next(2, 5);

            var vector = new[] { (double)workers, (double)modelVolume, (double)changes, (double)collisions, (double)approvalDelay, (double)rework };

            patterns.Add(new EventPattern
            {
                Id = Guid.NewGuid(),
                Name = $"Documentation Returned {i + 1}",
                Vector = JsonSerializer.Serialize(vector),
                EventType = EventType.DocumentationReturned,
                EventTypeName = nameof(EventType.DocumentationReturned),
                AverageDelayImpact = 1.15,
                CreatedAt = DateTime.UtcNow
            });
        }
        return patterns;
    }

    /// <summary>
    /// ExpertReviewFailed: высокий rework (4-11), критическая задержка (7-12)
    /// Симуляция: BaseRework += Random(4, 8), TemporaryApprovalDelay = Random(7, 13)
    /// </summary>
    private static List<EventPattern> GenerateExpertReviewFailed(int count)
    {
        var patterns = new List<EventPattern>();
        for (int i = 0; i < count; i++)
        {
            var workers = Random.Next(4, 9);
            var modelVolume = Random.Next(350, 601);
            var baseChanges = Random.Next(0, 3);
            var baseCollisions = Random.Next(0, 3);
            var baseRework = Random.Next(0, 3);

            // Событие: провал экспертизы
            var changes = baseChanges + Random.Next(1, 3);
            var collisions = baseCollisions + Random.Next(0, 2);
            var rework = baseRework + Random.Next(4, 8);
            var approvalDelay = Random.Next(7, 13);

            var vector = new[] { (double)workers, (double)modelVolume, (double)changes, (double)collisions, (double)approvalDelay, (double)rework };

            patterns.Add(new EventPattern
            {
                Id = Guid.NewGuid(),
                Name = $"Expert Review Failed {i + 1}",
                Vector = JsonSerializer.Serialize(vector),
                EventType = EventType.ExpertReviewFailed,
                EventTypeName = nameof(EventType.ExpertReviewFailed),
                AverageDelayImpact = 1.20,
                CreatedAt = DateTime.UtcNow
            });
        }
        return patterns;
    }

    /// <summary>
    /// ResourceShortage: низкий workersCount (1-3)
    /// Симуляция: workersCount = Math.Max(1, baseValue - Random(2, 4))
    /// </summary>
    private static List<EventPattern> GenerateResourceShortage(int count)
    {
        var patterns = new List<EventPattern>();
        for (int i = 0; i < count; i++)
        {
            // При нехватке ресурсов работников мало
            var workers = Random.Next(1, 4);
            var modelVolume = Random.Next(350, 601);
            var changes = Random.Next(0, 3);
            var collisions = Random.Next(0, 3);
            var rework = Random.Next(0, 2);

            // Событие: нехватка ресурсов может вызвать задержку
            var approvalDelay = Random.Next(0, 6);

            var vector = new[] { (double)workers, (double)modelVolume, (double)changes, (double)collisions, (double)approvalDelay, (double)rework };

            patterns.Add(new EventPattern
            {
                Id = Guid.NewGuid(),
                Name = $"Resource Shortage {i + 1}",
                Vector = JsonSerializer.Serialize(vector),
                EventType = EventType.ResourceShortage,
                EventTypeName = nameof(EventType.ResourceShortage),
                AverageDelayImpact = 1.14,
                CreatedAt = DateTime.UtcNow
            });
        }
        return patterns;
    }
}
