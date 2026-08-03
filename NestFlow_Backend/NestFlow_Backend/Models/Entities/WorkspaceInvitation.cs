using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class WorkspaceInvitation
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>邀請碼的 SHA-256 雜湊。明文只在產生當下回給建立者一次。</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Workspace? Workspace { get; set; }
}
