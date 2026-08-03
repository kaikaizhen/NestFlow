using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class WorkspaceMembership
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public Guid UserId { get; set; }

    public MembershipType MembershipType { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public DateTimeOffset JoinedAt { get; set; }

    public Workspace? Workspace { get; set; }

    public User? User { get; set; }
}
