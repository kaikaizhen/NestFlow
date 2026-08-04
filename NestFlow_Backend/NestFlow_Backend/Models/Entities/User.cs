using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class User
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    /// <summary>LINE 訊息預設寫入的 Workspace。首次登入時指向自動建立的個人 Workspace。</summary>
    public Guid? DefaultWorkspaceId { get; set; }

    /// <summary>
    /// 使用者時區的 IANA 名稱。資料一律以 UTC 保存，顯示與月份切分由前端依此欄位換算。
    /// </summary>
    public string TimeZone { get; set; } = "Asia/Taipei";

    /// <summary>
    /// 提醒通知總開關。關閉時 Worker 只略過發送，行程上的個別提醒設定不受影響，
    /// 重新開啟後不會補發已略過的過期通知。
    /// </summary>
    public bool NotificationsEnabled { get; set; } = true;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = [];

    public ICollection<WorkspaceMembership> Memberships { get; set; } = [];
}
