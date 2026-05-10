using CoreServer.Domain.Enums;

namespace CoreServer.Domain.Entities;

public class WorkMarkovState
{
    public Guid WorkId { get; set; }
    public WorkStabilityState CurrentState { get; set; }
    public DateTime LastUpdated { get; set; }
}
