using DigitalTwin.Application.Interfaces;
using DigitalTwin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Persistence.Repositories;

public class ProjectRepository(DigitalTwinDbContext dbContext) : IProjectRepository
{
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Projects
            .Include(x => x.Works)
            .ThenInclude(x => x.ParentDependencies)
            .Include(x => x.Works)
            .ThenInclude(x => x.ChildDependencies)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default) =>
        dbContext.Projects
            .Include(x => x.Works)
            .ThenInclude(x => x.ChildDependencies)
            .Include(x => x.Works)
            .ThenInclude(x => x.ParentDependencies)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default) =>
        await dbContext.Projects.AddAsync(project, cancellationToken);

    public Task DeleteAsync(Project project, CancellationToken cancellationToken = default)
    {
        dbContext.Projects.Remove(project);
        return Task.CompletedTask;
    }
}
