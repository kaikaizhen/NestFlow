using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public interface ISessionRepository
{
    Task AddAsync(Session session, CancellationToken cancellationToken);

    /// <summary>依 Token 雜湊取得尚未撤銷的 Session，包含使用者資料。</summary>
    Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task RevokeAsync(string tokenHash, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
