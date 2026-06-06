using CoreServer.Domain.Enums;

namespace CoreServer.Domain.Entities;

public class EventPattern
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Vector { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    // Free-form label used as the SVM training class. For seeded/known patterns this
    // equals EventType.ToString(). For user-defined patterns it holds whatever the
    // user typed, enabling new event types beyond the fixed enum.
    public string EventTypeName { get; set; } = string.Empty;
    public double AverageDelayImpact { get; set; }
    public DateTime CreatedAt { get; set; }
}
