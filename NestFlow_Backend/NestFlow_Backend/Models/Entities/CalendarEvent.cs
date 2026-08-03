using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class CalendarEvent
{
    public Guid Id { get; set; }

    /// <summary>建立這筆行程的使用者。家庭空間內任何成員都可修改，此欄位僅供顯示與追溯。</summary>
    public Guid UserId { get; set; }

    public Guid WorkspaceId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>開始時間，一律以 UTC 保存，由前端依使用者時區換算。</summary>
    public DateTimeOffset StartAt { get; set; }

    /// <summary>結束時間，必須晚於開始時間。</summary>
    public DateTimeOffset EndAt { get; set; }

    public CalendarEventStatus Status { get; set; } = CalendarEventStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }

    public Workspace? Workspace { get; set; }
}
