using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class User
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    /// <summary>LINE 訊息預設寫入的 Workspace。首次登入時指向自動建立的個人 Workspace。</summary>
    public Guid? DefaultWorkspaceId { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = [];

    public ICollection<WorkspaceMembership> Memberships { get; set; } = [];
}
