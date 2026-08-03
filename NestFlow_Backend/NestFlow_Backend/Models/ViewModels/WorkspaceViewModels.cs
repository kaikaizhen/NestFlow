namespace NestFlow_Backend.Models.ViewModels;

public class WorkspaceViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string MembershipType { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class WorkspaceMemberViewModel
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    public string MembershipType { get; set; } = string.Empty;

    public DateTimeOffset JoinedAt { get; set; }
}

public class InvitationViewModel
{
    public string Code { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}

public class CurrentUserViewModel
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    public Guid? DefaultWorkspaceId { get; set; }

    public string TimeZone { get; set; } = string.Empty;

    public bool IsLineLinked { get; set; }
}
