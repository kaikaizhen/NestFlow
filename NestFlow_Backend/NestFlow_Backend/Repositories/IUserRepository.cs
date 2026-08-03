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

    /// <summary>
    /// 不限 Channel 查找。用於 LINE Login 與 Messaging API 屬同一 Provider 時，
    /// 以 Messaging 的使用者 ID 找到既有帳號。
    /// </summary>
    Task<User?> GetByExternalSubjectHashAnyChannelAsync(
        IdentityProvider provider,
        string externalSubjectHash,
        CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task AddExternalIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken);

    Task<ExternalIdentity?> GetExternalIdentityAsync(Guid userId, IdentityProvider provider, CancellationToken cancellationToken);

    /// <summary>列出使用者在該 Provider 下所有有效的外部身分，供推播挑選收件者。</summary>
    Task<List<ExternalIdentity>> ListExternalIdentitiesAsync(
        Guid userId,
        IdentityProvider provider,
        CancellationToken cancellationToken);

    /// <summary>判斷使用者是否已在指定 Channel 建立外部身分，用於顯示綁定狀態。</summary>
    Task<bool> HasExternalIdentityAsync(
        Guid userId,
        IdentityProvider provider,
        string channelId,
        CancellationToken cancellationToken);
}
