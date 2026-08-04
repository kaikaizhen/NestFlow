using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class Todo
{
    public Guid Id { get; set; }

    /// <summary>建立這筆代辦的使用者。家庭空間內任何成員都可修改，此欄位僅供顯示與追溯。</summary>
    public Guid UserId { get; set; }

    public Guid WorkspaceId { get; set; }

    public TodoType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>購物清單的數量。一般代辦一律為 null。</summary>
    public int? Quantity { get; set; }

    /// <summary>到期時間，一律以 UTC 保存。未設定為 null。</summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>完成時間。null 代表未完成，取消完成時清回 null。</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public TodoStatus Status { get; set; } = TodoStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }

    public Workspace? Workspace { get; set; }
}
