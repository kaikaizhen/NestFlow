using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly NestFlowDbContext _dbContext;

    public SessionRepository(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Session session, CancellationToken cancellationToken)
    {
        await _dbContext.Sessions.AddAsync(session, cancellationToken);
    }

    public Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return _dbContext.Sessions
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null, cancellationToken);
    }

    public async Task RevokeAsync(string tokenHash, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var session = await _dbContext.Sessions
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null, cancellationToken);

        if (session is not null)
        {
            session.RevokedAt = revokedAt;
        }
    }
}
