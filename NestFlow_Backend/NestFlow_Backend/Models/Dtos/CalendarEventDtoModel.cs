using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Dtos;

public class CalendarEventDtoModel
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    /// <summary>是否設有提醒。列表查詢與單筆查詢皆會正確填入。</summary>
    public bool HasReminder { get; set; }

    /// <summary>提前幾分鐘通知，只有 HasReminder 為 true 時才有值。</summary>
    public int? ReminderMinutesBeforeStart { get; set; }

    /// <summary>是否屬於週期行程系列。</summary>
    public bool IsRecurring { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;
}

/// <summary>
/// 行程寫入用的命令，欄位皆已通過驗證與正規化。
/// 週期相關欄位只在 <see cref="Services.ICalendarEventService.CreateAsync"/> 生效，
/// 更新行程時規則不可變更。
/// </summary>
public record SaveCalendarEventCommand(
    string Title,
    string? Description,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    bool WantsReminder = false,
    int? ReminderMinutesBeforeStart = null,
    CalendarRecurrenceEndType? RecurrenceEndType = null,
    int? RecurrenceCount = null,
    DateTimeOffset? RecurrenceUntilUtc = null);
