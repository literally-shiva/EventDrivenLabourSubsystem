using CoreServer.Domain.Enums;

namespace CoreServer.Domain.Entities;

public class DetectedEvent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid WorkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    public bool IsKnown { get; set; }
    public double Confidence { get; set; }
    public DateTime Timestamp { get; set; }
    /// <summary>JSON-serialised double[] — the ML feature vector of this event candidate.</summary>
    public string FeatureVector { get; set; } = string.Empty;
}
