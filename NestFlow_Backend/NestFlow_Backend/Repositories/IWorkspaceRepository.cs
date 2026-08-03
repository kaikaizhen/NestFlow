using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public interface IWorkspaceRepository
{
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken);

    Task<Workspace?> GetActiveAsync(Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>取得使用者仍有有效成員資格的所有 Workspace。</summary>
    Task<List<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task AddMembershipAsync(WorkspaceMembership membership, CancellationToken cancellationToken);

    /// <summary>取得有效成員資格，找不到代表無權存取該 Workspace。</summary>
    Task<WorkspaceMembership?> GetActiveMembershipAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);

    Task<List<WorkspaceMembership>> ListMembersAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task AddInvitationAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken);

    Task<WorkspaceInvitation?> GetInvitationByCodeHashAsync(string codeHash, CancellationToken cancellationToken);
}
