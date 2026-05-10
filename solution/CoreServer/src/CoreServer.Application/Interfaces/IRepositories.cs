using CoreServer.Domain.Entities;

namespace CoreServer.Application.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Project project, CancellationToken cancellationToken = default);
}

public interface IWorkRepository
{
    Task<Work?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddOrUpdateAsync(Work work, CancellationToken cancellationToken = default);
    Task<List<Work>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface IMetricRepository
{
    Task AddRangeAsync(IEnumerable<MetricHistory> items, CancellationToken cancellationToken = default);
    Task<List<MetricHistory>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface IEventPatternRepository
{
    Task AddAsync(EventPattern pattern, CancellationToken cancellationToken = default);
    Task<List<EventPattern>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface IDetectedEventRepository
{
    Task AddAsync(DetectedEvent detectedEvent, CancellationToken cancellationToken = default);
    Task<List<DetectedEvent>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface IWorkMarkovStateRepository
{
    Task<WorkMarkovState?> GetAsync(Guid workId, CancellationToken cancellationToken = default);
    Task AddOrUpdateAsync(WorkMarkovState state, CancellationToken cancellationToken = default);
}

public interface IDurationHistoryRepository
{
    Task AddAsync(DurationHistory item, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
