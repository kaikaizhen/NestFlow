using NestFlow_Backend.Models.Dtos;

namespace NestFlow_Backend.Services;

public interface ICalendarEventService
{
    /// <summary>取得單筆行程，供編輯畫面使用。</summary>
    Task<CalendarEventDtoModel> GetAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);

    Task<CalendarEventDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveCalendarEventCommand command,
        CancellationToken cancellationToken);

    Task<CalendarEventDtoModel> UpdateAsync(
        Guid userId,
        Guid eventId,
        Guid workspaceId,
        SaveCalendarEventCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// 軟刪除單一場次（週期行程的「僅此次取消」）。資料保留於資料庫但不再出現於任何列表。
    /// </summary>
    Task DeleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// 刪除整個週期系列中尚未發生的場次；已過去的場次保留作為歷史紀錄。
    /// 若該行程並非週期行程，效果等同 <see cref="DeleteAsync"/>。
    /// </summary>
    Task DeleteSeriesAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);

    Task<List<CalendarEventDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken);
}
