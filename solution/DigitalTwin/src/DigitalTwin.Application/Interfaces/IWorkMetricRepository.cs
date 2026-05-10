using DigitalTwin.Domain.Entities;

namespace DigitalTwin.Application.Interfaces;

public interface IWorkMetricRepository
{
    Task AddRangeAsync(IEnumerable<WorkMetricSnapshot> metrics, CancellationToken cancellationToken = default);
    Task<List<WorkMetricSnapshot>> GetLatestByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
