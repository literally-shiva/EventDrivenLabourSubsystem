using CoreServer.Application.Interfaces;
using CoreServer.Domain.Entities;
using CoreServer.Domain.Enums;

namespace CoreServer.Infrastructure.Services;

public class DurationRecalculationEngine(
    IWorkRepository workRepository,
    IWorkMarkovStateRepository stateRepository,
    IDurationHistoryRepository durationHistoryRepository,
    IUnitOfWork unitOfWork) : IDurationRecalculationEngine
{
    public async Task<double> RecalculateAsync(Guid workId, Guid eventId, string eventType, CancellationToken cancellationToken = default)
    {
        var work = await workRepository.GetAsync(workId, cancellationToken) ?? throw new InvalidOperationException("Work not found.");
        var state = await stateRepository.GetAsync(workId, cancellationToken);
        var stateImpact = state?.CurrentState switch
        {
            WorkStabilityState.S1LowSensitive => 1.05,
            WorkStabilityState.S2MediumSensitive => 1.12,
            WorkStabilityState.S3HighSensitive => 1.22,
            WorkStabilityState.S4Critical => 1.35,
            _ => 1.0
        };

        var eventImpact = eventType switch
        {
            nameof(EventType.ApprovalDelayed) => 1.18,
            nameof(EventType.DocumentationReturned) => 1.15,
            nameof(EventType.CollisionDetected) => 1.12,
            nameof(EventType.ResourceShortage) => 1.14,
            nameof(EventType.ExpertReviewFailed) => 1.20,
            _ => 1.08
        };

        var previousDuration = work.CurrentDuration <= 0 ? work.PlannedDuration : work.CurrentDuration;
        var newDuration = Math.Round(previousDuration * eventImpact * stateImpact, 2);
        work.CurrentDuration = newDuration;

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
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return newDuration;
    }
}
