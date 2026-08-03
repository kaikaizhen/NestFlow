using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>依外部身分的確定性雜湊查找本地使用者。</summary>
    Task<User?> GetByExternalSubjectHashAsync(
        IdentityProvider provider,
        string channelId,
        string externalSubjectHash,
        CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task AddExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken);

    Task<ExternalIdentity?> GetExternalIdentityAsync(Guid userId, IdentityProvider provider, CancellationToken cancellationToken);
}
