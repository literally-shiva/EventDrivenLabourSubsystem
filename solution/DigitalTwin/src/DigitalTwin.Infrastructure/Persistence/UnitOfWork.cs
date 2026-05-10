using DigitalTwin.Application.Interfaces;

namespace DigitalTwin.Infrastructure.Persistence;

public class UnitOfWork(DigitalTwinDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
