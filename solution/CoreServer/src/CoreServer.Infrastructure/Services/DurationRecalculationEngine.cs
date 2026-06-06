using CoreServer.Application.Interfaces;
using CoreServer.Domain.Entities;
using CoreServer.Domain.Enums;

namespace CoreServer.Infrastructure.Services;

public class DurationRecalculationEngine(
    IWorkRepository workRepository,
    IWorkMarkovStateRepository stateRepository,
    IDurationHistoryRepository durationHistoryRepository) : IDurationRecalculationEngine
{
    public async Task<double> RecalculateAsync(Guid workId, Guid eventId, string eventType, CancellationToken cancellationToken = default)
    {
        var work = await workRepository.GetAsync(workId, cancellationToken) ?? throw new InvalidOperationException("Work not found.");
        var state = await stateRepository.GetAsync(workId, cancellationToken);
        var stateImpact = state?.CurrentState switch
        {
            WorkStabilityState.S1LowSensitive    => 1.1,
            WorkStabilityState.S2MediumSensitive => 1.3,
            WorkStabilityState.S3HighSensitive   => 1.6,
            WorkStabilityState.S4Critical        => 2.0,
            _                                    => 1.0
        };

        var eventImpact = eventType switch
        {
            nameof(EventType.ApprovalDelayed)          => 1.18,
            nameof(EventType.DocumentationReturned)    => 1.15,
            nameof(EventType.CollisionDetected)        => 1.12,
            nameof(EventType.ResourceShortage)         => 1.14,
            nameof(EventType.ExpertReviewFailed)       => 1.20,
            nameof(EventType.DesignRequirementChanged) => 1.10,
            _                                          => 1.08
        };

        var previousDuration = work.CurrentDuration <= 0 ? work.PlannedDuration : work.CurrentDuration;
        var raw = Math.Round(previousDuration * eventImpact * stateImpact, 2);
        // Cap at 3× planned duration so runaway compound events stay realistic
        var newDuration = Math.Min(raw, work.PlannedDuration * 3.0);
        work.CurrentDuration = newDuration;

        // Обновление EndDate на основе новой длительности
        work.EndDate = work.StartDate.AddDays(newDuration);

        await durationHistoryRepository.AddAsync(new DurationHistory
        {
            Id = Guid.NewGuid(),
            WorkId = workId,
            PreviousDuration = previousDuration,
            NewDuration = newDuration,
            EventId = eventId,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);

        await workRepository.AddOrUpdateAsync(work, cancellationToken);
        // SaveChanges is intentionally omitted here: MetricsProcessingService owns
        // the transaction boundary and calls SaveChanges once after all candidates.
        return newDuration;
    }
}
