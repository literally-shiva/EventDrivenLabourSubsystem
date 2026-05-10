namespace CoreServer.Domain.Entities;

public class DurationHistory
{
    public Guid Id { get; set; }
    public Guid WorkId { get; set; }
    public double PreviousDuration { get; set; }
    public double NewDuration { get; set; }
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
}
