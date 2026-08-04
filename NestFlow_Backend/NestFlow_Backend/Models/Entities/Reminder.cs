using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class Reminder
{
    public Guid Id { get; set; }

    /// <summary>建立提醒的使用者，也是通知的收件者。</summary>
    public Guid UserId { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// 由行程建立的提醒會指向來源行程；使用者手動建立的自由提醒則為 null。
    /// 一筆行程最多對應一筆未取消的提醒。
    /// </summary>
    public Guid? CalendarEventId { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>觸發時間，一律以 UTC 保存。</summary>
    public DateTimeOffset TriggerAt { get; set; }

    /// <summary>通知管道。第一版只有 LINE。</summary>
    public IdentityProvider NotificationProvider { get; set; } = IdentityProvider.Line;

    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;

    /// <summary>已嘗試發送但失敗的次數，達上限後轉為 Failed 不再重試。</summary>
    public int RetryCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }

    public Workspace? Workspace { get; set; }
}
