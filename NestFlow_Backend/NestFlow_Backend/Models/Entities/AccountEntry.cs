using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class AccountEntry
{
    public Guid Id { get; set; }

    /// <summary>建立這筆記帳的使用者。家庭空間內任何成員都可修改，此欄位僅供顯示與追溯。</summary>
    public Guid UserId { get; set; }

    public Guid WorkspaceId { get; set; }

    public EntryType Type { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = SupportedCurrencies.Default;

    /// <summary>分類代碼，對應 <see cref="AccountCategories"/>。</summary>
    public string Category { get; set; } = string.Empty;

    public string? Note { get; set; }

    /// <summary>發生時間，一律以 UTC 保存，由前端依使用者時區換算。</summary>
    public DateTimeOffset OccurredAt { get; set; }

    public EntryStatus Status { get; set; } = EntryStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }

    public Workspace? Workspace { get; set; }
}
