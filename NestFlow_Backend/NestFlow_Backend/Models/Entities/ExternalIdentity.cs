using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class ExternalIdentity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public IdentityProvider Provider { get; set; }

    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// 外部使用者識別碼密文（AES-256-GCM）。屬高敏感資料，不以明文保存。
    /// </summary>
    public string ExternalSubject { get; set; } = string.Empty;

    /// <summary>
    /// 外部使用者識別碼的確定性雜湊，供登入時查找使用。
    /// 密文每次加密結果都不同，無法用於查詢，因此另存此欄位。
    /// </summary>
    public string ExternalSubjectHash { get; set; } = string.Empty;

    public ExternalIdentityStatus Status { get; set; } = ExternalIdentityStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}
