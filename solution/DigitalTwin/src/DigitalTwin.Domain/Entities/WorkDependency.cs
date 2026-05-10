namespace DigitalTwin.Domain.Entities;

public class WorkDependency
{
    public Guid ParentWorkId { get; set; }
    public Guid ChildWorkId { get; set; }
    public Work? ParentWork { get; set; }
    public Work? ChildWork { get; set; }
}
