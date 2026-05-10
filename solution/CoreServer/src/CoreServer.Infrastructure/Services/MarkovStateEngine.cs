using CoreServer.Application.Interfaces;
using CoreServer.Domain.Entities;
using CoreServer.Domain.Enums;

namespace CoreServer.Infrastructure.Services;

public class MarkovStateEngine(IWorkMarkovStateRepository stateRepository, IUnitOfWork unitOfWork) : IMarkovStateEngine
{
    private static readonly double[,] Matrix =
    {
        { 0.70, 0.20, 0.07, 0.02, 0.01 },
        { 0.15, 0.50, 0.20, 0.10, 0.05 },
        { 0.10, 0.20, 0.35, 0.20, 0.15 },
        { 0.05, 0.10, 0.20, 0.35, 0.30 },
        { 0.02, 0.05, 0.08, 0.20, 0.65 }
    };

    public async Task<string> ApplyEventAsync(Guid workId, string eventType, CancellationToken cancellationToken = default)
    {
        var current = await stateRepository.GetAsync(workId, cancellationToken) ?? new WorkMarkovState
        {
            WorkId = workId,
            CurrentState = WorkStabilityState.S0Stable,
            LastUpdated = DateTime.UtcNow
        };

        var row = (int)current.CurrentState;
        var nextStateIndex = SampleState(row, eventType);
        current.CurrentState = (WorkStabilityState)nextStateIndex;
        current.LastUpdated = DateTime.UtcNow;

        await stateRepository.AddOrUpdateAsync(current, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return current.CurrentState.ToString();
    }

    private static int SampleState(int row, string eventType)
    {
        var severityBoost = eventType.Contains("Failed", StringComparison.OrdinalIgnoreCase) || eventType.Contains("Delayed", StringComparison.OrdinalIgnoreCase) ? 0.12 : 0.05;
        var random = Random.Shared.NextDouble();
        var cumulative = 0d;
        for (var i = 0; i < 5; i++)
        {
            var probability = Matrix[row, i] + (i > row ? severityBoost / 4 : 0);
            cumulative += probability;
            if (random <= cumulative)
            {
                return i;
            }
        }

        return 4;
    }
}
