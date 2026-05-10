using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Persistence.Repositories;

public class WorkMetricRepository(DigitalTwinDbContext dbContext) : IWorkMetricRepository
{
    public async Task AddRangeAsync(IEnumerable<WorkMetricSnapshot> metrics, CancellationToken cancellationToken = default) =>
        await dbContext.WorkMetricSnapshots.AddRangeAsync(metrics, cancellationToken);

    public async Task<List<WorkMetricSnapshot>> GetLatestByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkMetricSnapshots
            .Include(x => x.Work)
            .Where(x => x.Work != null && x.Work.ProjectId == projectId)
            .GroupBy(x => x.WorkId)
            .Select(x => x.OrderByDescending(m => m.Timestamp).First())
            .ToListAsync(cancellationToken);
    }
}
