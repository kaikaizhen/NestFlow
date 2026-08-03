using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public interface IReminderRepository
{
    Task AddAsync(Reminder reminder, CancellationToken cancellationToken);

    /// <summary>取得尚未取消的單筆提醒。</summary>
    Task<Reminder?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>依 Workspace 取得區間內的提醒，依觸發時間由早到晚排序。已取消的不列出。</summary>
    Task<List<Reminder>> ListAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// 取出到期且待發送的提醒，並以單一 UPDATE 原子性標記為 Sending。
    /// 只有真正搶到的那一次會回傳該筆，確保同一則提醒不會重複發送。
    /// </summary>
    Task<List<Reminder>> ClaimDueAsync(
        DateTimeOffset nowUtc,
        int maxCount,
        CancellationToken cancellationToken);
}
