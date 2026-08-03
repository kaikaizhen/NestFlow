using NestFlow_Backend.Data;

namespace NestFlow_Backend.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NestFlowDbContext _dbContext;

    public UnitOfWork(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
