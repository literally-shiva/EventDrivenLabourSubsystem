namespace CoreServer.Domain.Entities;

public class MetricHistory
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid WorkId { get; set; }
    public string WorkName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int WorkersCount { get; set; }
    public double ModelDataVolume { get; set; }
    public int ChangesCount { get; set; }
    public int CollisionCount { get; set; }
    public int ApprovalCount { get; set; }
    public int ApprovalDelayDays { get; set; }
    public int DocumentationVersionCount { get; set; }
    public int ReworkCount { get; set; }
    public double ProgressPercent { get; set; }
    public string SimulatedEventType { get; set; } = string.Empty;
}
