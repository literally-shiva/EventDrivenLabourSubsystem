namespace DigitalTwin.Application.DTOs;

public record ProjectDto(
    Guid Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CurrentSimulationTime,
    bool IsSimulationRunning,
    IReadOnlyCollection<WorkDto> Works,
    IReadOnlyCollection<WorkDependencyDto> Dependencies);

public record WorkDto(Guid Id, Guid ProjectId, string Name, DateTime StartDate, DateTime EndDate, int PlannedDuration, int CurrentDuration, double PercentComplete, bool IsCompleted);

public record WorkDependencyDto(Guid SourceWorkId, Guid TargetWorkId);

public record SimulationStateDto(Guid ProjectId, bool IsRunning, DateTime CurrentSimulationTime, IReadOnlyCollection<WorkMetricDto> LatestMetrics);

public record WorkMetricDto(
    Guid WorkId,
    DateTime Timestamp,
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
