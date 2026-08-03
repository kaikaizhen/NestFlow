using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class Workspace
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public WorkspaceType Type { get; set; }

    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<WorkspaceMembership> Memberships { get; set; } = [];
}
