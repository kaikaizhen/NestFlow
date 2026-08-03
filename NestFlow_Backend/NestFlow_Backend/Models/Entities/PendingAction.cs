using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

/// <summary>
/// 由外部訊息解析出、等待使用者確認的動作。確認前不會寫入正式資料。
/// </summary>
public class PendingAction
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid WorkspaceId { get; set; }

    public IdentityProvider Provider { get; set; }

    public PendingActionType ActionType { get; set; }

    /// <summary>已驗證的動作內容，以 JSON 保存。</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>解析來源版本。固定格式解析為 fixed-format-v1，Dify 解析於 Module 8 起填入。</summary>
    public string WorkflowVersion { get; set; } = string.Empty;

    public PendingActionStatus Status { get; set; } = PendingActionStatus.Pending;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
