using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public interface ICalendarEventRepository
{
    Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken);

    /// <summary>取得未刪除的單筆行程。</summary>
    Task<CalendarEvent?> GetActiveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 取得同一週期系列中尚未刪除的所有場次。
    /// 供整系列更新（僅未來場次）與整系列刪除使用。
    /// </summary>
    Task<List<CalendarEvent>> ListActiveBySeriesAsync(
        Guid recurrenceGroupId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 依 Workspace 與 UTC 時間區間取得行程，依開始時間由早到晚排序。
    /// 只要行程與區間有重疊就會取得，跨日或跨月的行程才不會在月曆上消失。
    /// </summary>
    Task<List<CalendarEvent>> ListAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken);
}
