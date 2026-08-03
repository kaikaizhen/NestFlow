using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class UserRepository : IUserRepository
{
    private readonly NestFlowDbContext _dbContext;

    public UserRepository(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<User?> GetByExternalSubjectHashAsync(
        IdentityProvider provider,
        string channelId,
        string externalSubjectHash,
        CancellationToken cancellationToken)
    {
        var identity = await _dbContext.ExternalIdentities
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.Provider == provider
                    && x.ChannelId == channelId
                    && x.ExternalSubjectHash == externalSubjectHash
                    && x.Status == ExternalIdentityStatus.Active,
                cancellationToken);

        return identity?.User;
    }

    public async Task<User?> GetByExternalSubjectHashAnyChannelAsync(
        IdentityProvider provider,
        string externalSubjectHash,
        CancellationToken cancellationToken)
    {
        var identity = await _dbContext.ExternalIdentities
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.Provider == provider
                    && x.ExternalSubjectHash == externalSubjectHash
                    && x.Status == ExternalIdentityStatus.Active,
                cancellationToken);

        return identity?.User;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    public async Task AddExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken)
    {
        await _dbContext.ExternalIdentities.AddAsync(identity, cancellationToken);
    }

    public Task<ExternalIdentity?> GetExternalIdentityAsync(
        Guid userId,
        IdentityProvider provider,
        CancellationToken cancellationToken)
    {
        return _dbContext.ExternalIdentities.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Provider == provider && x.Status == ExternalIdentityStatus.Active,
            cancellationToken);
    }

    public Task<bool> HasExternalIdentityAsync(
        Guid userId,
        IdentityProvider provider,
        string channelId,
        CancellationToken cancellationToken)
    {
        return _dbContext.ExternalIdentities.AnyAsync(
            x => x.UserId == userId
                && x.Provider == provider
                && x.ChannelId == channelId
                && x.Status == ExternalIdentityStatus.Active,
            cancellationToken);
    }
}
