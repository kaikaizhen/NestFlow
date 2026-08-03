using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Dtos;

public class WorkspaceDtoModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public WorkspaceType Type { get; set; }

    public MembershipType MembershipType { get; set; }

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class WorkspaceMemberDtoModel
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    public MembershipType MembershipType { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}

public class InvitationDtoModel
{
    /// <summary>邀請碼明文，只在產生當下回傳一次，資料庫僅保存雜湊。</summary>
    public string Code { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}

public class CurrentUserDtoModel
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    public Guid? DefaultWorkspaceId { get; set; }

    public bool IsLineLinked { get; set; }
}
