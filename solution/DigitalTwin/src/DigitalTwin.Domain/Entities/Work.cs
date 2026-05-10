namespace DigitalTwin.Domain.Entities;

public class Work
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int PlannedDuration { get; set; }
    public int CurrentDuration { get; set; }
    public double PercentComplete { get; set; }
    public bool IsCompleted { get; set; }
    public Project? Project { get; set; }
    public ICollection<WorkDependency> ParentDependencies { get; set; } = new List<WorkDependency>();
    public ICollection<WorkDependency> ChildDependencies { get; set; } = new List<WorkDependency>();
    public ICollection<WorkMetricSnapshot> MetricSnapshots { get; set; } = new List<WorkMetricSnapshot>();
}
