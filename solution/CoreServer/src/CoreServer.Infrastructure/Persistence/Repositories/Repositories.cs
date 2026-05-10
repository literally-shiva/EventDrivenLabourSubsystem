using CoreServer.Application.Interfaces;
using CoreServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreServer.Infrastructure.Persistence.Repositories;

public class ProjectRepository(CoreServerDbContext dbContext) : IProjectRepository
{
    public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Projects.Include(x => x.Works).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default) =>
        dbContext.Projects.Include(x => x.Works).ToListAsync(cancellationToken);

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default) =>
        await dbContext.Projects.AddAsync(project, cancellationToken);
}

public class WorkRepository(CoreServerDbContext dbContext) : IWorkRepository
{
    public Task<Work?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Works.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddOrUpdateAsync(Work work, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(work.Id, cancellationToken);
        if (existing is null)
        {
            await dbContext.Works.AddAsync(work, cancellationToken);
            return;
        }

        dbContext.Entry(existing).CurrentValues.SetValues(work);
    }

    public Task<List<Work>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        dbContext.Works.Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
}

public class MetricRepository(CoreServerDbContext dbContext) : IMetricRepository
{
    public async Task AddRangeAsync(IEnumerable<MetricHistory> items, CancellationToken cancellationToken = default) =>
        await dbContext.MetricsHistory.AddRangeAsync(items, cancellationToken);

    public Task<List<MetricHistory>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        dbContext.MetricsHistory.Where(x => x.ProjectId == projectId).OrderBy(x => x.Timestamp).ToListAsync(cancellationToken);
}

public class EventPatternRepository(CoreServerDbContext dbContext) : IEventPatternRepository
{
    public async Task AddAsync(EventPattern pattern, CancellationToken cancellationToken = default) =>
        await dbContext.EventPatterns.AddAsync(pattern, cancellationToken);

    public Task<List<EventPattern>> GetAllAsync(CancellationToken cancellationToken = default) =>
        dbContext.EventPatterns.OrderBy(x => x.Name).ToListAsync(cancellationToken);
}

public class DetectedEventRepository(CoreServerDbContext dbContext) : IDetectedEventRepository
{
    public async Task AddAsync(DetectedEvent detectedEvent, CancellationToken cancellationToken = default) =>
        await dbContext.DetectedEvents.AddAsync(detectedEvent, cancellationToken);

    public Task<List<DetectedEvent>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        dbContext.DetectedEvents.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.Timestamp).ToListAsync(cancellationToken);
}

public class WorkMarkovStateRepository(CoreServerDbContext dbContext) : IWorkMarkovStateRepository
{
    public Task<WorkMarkovState?> GetAsync(Guid workId, CancellationToken cancellationToken = default) =>
        dbContext.WorkMarkovStates.FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);

    public async Task AddOrUpdateAsync(WorkMarkovState state, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(state.WorkId, cancellationToken);
        if (existing is null)
        {
            await dbContext.WorkMarkovStates.AddAsync(state, cancellationToken);
            return;
        }

        dbContext.Entry(existing).CurrentValues.SetValues(state);
    }
}

public class DurationHistoryRepository(CoreServerDbContext dbContext) : IDurationHistoryRepository
{
    public async Task AddAsync(DurationHistory item, CancellationToken cancellationToken = default) =>
        await dbContext.DurationHistory.AddAsync(item, cancellationToken);
}

public class UnitOfWork(CoreServerDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
