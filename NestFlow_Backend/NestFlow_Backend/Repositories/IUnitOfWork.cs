namespace NestFlow_Backend.Repositories;

/// <summary>
/// 由 Service 層決定交易邊界，Repository 只負責組裝變更。
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
