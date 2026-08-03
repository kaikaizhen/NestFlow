using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

/// <summary>
/// 外部通訊軟體的身分綁定碼。當外部帳號無法自動對應到本地使用者時使用。
/// 明文只在產生當下回給使用者一次，資料庫僅保存雜湊。
/// </summary>
public class BindingCode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public IdentityProvider Provider { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public BindingCodeStatus Status { get; set; } = BindingCodeStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }
}
