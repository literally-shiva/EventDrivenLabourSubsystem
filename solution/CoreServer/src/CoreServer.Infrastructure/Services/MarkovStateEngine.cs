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

        // Build raw probabilities, copying the matrix row first
        var probs = new double[5];
        for (var i = 0; i < 5; i++)
            probs[i] = Matrix[row, i];

        // Distribute boost toward higher states with exponential decay so that
        // the immediately next state benefits most, not all states equally.
        if (row < 4)
        {
            var remaining = severityBoost;
            for (var i = row + 1; i < 5 && remaining > 1e-9; i++)
            {
                var share = remaining * 0.6;
                probs[i] += share;
                remaining -= share;
            }
            // Any rounding remainder goes to the worst state
            if (remaining > 1e-9) probs[4] += remaining;
        }

        // Normalise so row sums to exactly 1.0 (prevents CDF exceeding 1.0)
        var sum = 0d;
        for (var i = 0; i < 5; i++) sum += probs[i];

        var random = Random.Shared.NextDouble();
        var cumulative = 0d;
        for (var i = 0; i < 5; i++)
        {
            cumulative += probs[i] / sum;
            if (random <= cumulative)
                return i;
        }

        return 4;
    }
}
