namespace CoreServer.Application.DTOs;

public record ProjectTimelineDto(Guid ProjectId, IReadOnlyCollection<WorkTimelineDto> Works, IReadOnlyCollection<DetectedEventDto> Events);

public record WorkTimelineDto(Guid Id, string Name, DateTime StartDate, DateTime EndDate, int PlannedDuration, double CurrentDuration, double PercentComplete, string CurrentState);
