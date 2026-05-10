using CoreServer.Domain.Enums;

namespace CoreServer.Domain.Entities;

public class EventPattern
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Vector { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    public double AverageDelayImpact { get; set; }
    public DateTime CreatedAt { get; set; }
}
