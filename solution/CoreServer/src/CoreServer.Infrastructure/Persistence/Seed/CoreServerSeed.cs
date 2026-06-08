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
            // Generate synthetic training data that matches what DigitalTwin simulation produces.
            // Feature vector order: [workersCount, modelDataVolume, changesCount, collisionCount, approvalDelayDays, reworkCount]
            //
            // We generate 25 samples per event type (150 total vectors) to give SVM enough data
            // to learn proper decision boundaries. This is significantly more robust than the
            // previous 18-vector seed (3 per class).
            //
            // The generator creates vectors that match the actual distribution from simulation:
            //   - CollisionDetected: collisions spike 4-15
            //   - DesignRequirementChanged: changes 3-12, rework 2-6 (not 0-1 as before)
            //   - ApprovalDelayed: delay 5-12 days
            //   - DocumentationReturned: rework 2-6, delay 2-4
            //   - ExpertReviewFailed: rework 4-11, delay 7-12 (not 4-6 as before)
            //   - ResourceShortage: workers 1-3

            var syntheticPatterns = SyntheticTrainingDataGenerator.GenerateTrainingData(samplesPerClass: 25);
            dbContext.EventPatterns.AddRange(syntheticPatterns);
        }

        await dbContext.SaveChangesAsync();
    }
}
