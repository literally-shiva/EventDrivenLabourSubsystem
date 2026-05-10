using CoreServer.Domain.Enums;

namespace CoreServer.Domain.Entities;

public class Work
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int PlannedDuration { get; set; }
    public double CurrentDuration { get; set; }
    public double PercentComplete { get; set; }
    public WorkStabilityState CurrentState { get; set; } = WorkStabilityState.S0Stable;
    public Project? Project { get; set; }
}
