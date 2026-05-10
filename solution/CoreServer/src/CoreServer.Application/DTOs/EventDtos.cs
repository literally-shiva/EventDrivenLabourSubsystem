namespace CoreServer.Application.DTOs;

public record EventPatternDto(Guid Id, string Name, string Vector, string EventType, double AverageDelayImpact, DateTime CreatedAt);

public record CreateEventPatternRequest(string Name, double[] Vector, string EventType, double AverageDelayImpact);

public record RegisterUnknownEventRequest(Guid WorkId, Guid ProjectId, string Name, double[] Vector);

public record DetectedEventDto(Guid Id, Guid ProjectId, Guid WorkId, string Name, string EventType, bool IsKnown, double Confidence, DateTime Timestamp);
