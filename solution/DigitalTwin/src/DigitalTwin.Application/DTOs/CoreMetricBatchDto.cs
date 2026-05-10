namespace DigitalTwin.Application.DTOs;

public record CoreMetricBatchDto(Guid ProjectId, DateTime Timestamp, IReadOnlyCollection<CoreMetricDto> Metrics);

public record CoreMetricDto(
    Guid WorkId,
    string WorkName,
    int WorkersCount,
    double ModelDataVolume,
    int ChangesCount,
    int CollisionCount,
    int ApprovalCount,
    int ApprovalDelayDays,
    int DocumentationVersionCount,
    int ReworkCount,
    double ProgressPercent,
    string SimulatedEventType);
