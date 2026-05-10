namespace CoreServer.Application.DTOs;

public record MetricsBatchRequest(Guid ProjectId, DateTime Timestamp, IReadOnlyCollection<MetricItemRequest> Metrics);

public record MetricItemRequest(
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
