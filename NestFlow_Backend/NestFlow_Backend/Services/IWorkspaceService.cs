using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Dtos;

namespace NestFlow_Backend.Services;

public interface IWorkspaceService
{
    Task<List<WorkspaceDtoModel>> ListAsync(Guid userId, CancellationToken cancellationToken);

    Task<WorkspaceDtoModel> CreateAsync(Guid userId, string name, WorkspaceType type, CancellationToken cancellationToken);

    Task<WorkspaceDtoModel> RenameAsync(Guid userId, Guid workspaceId, string name, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken);

    Task<InvitationDtoModel> CreateInvitationAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken);

    Task<WorkspaceDtoModel> JoinAsync(Guid userId, string code, CancellationToken cancellationToken);

    Task<List<WorkspaceMemberDtoModel>> ListMembersAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>建立者移除成員，或成員自行離開。</summary>
    Task RemoveMemberAsync(Guid actingUserId, Guid workspaceId, Guid targetUserId, CancellationToken cancellationToken);

    Task SetDefaultAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>驗證使用者是否為該 Workspace 的有效成員，供後續模組共用。</summary>
    Task EnsureMemberAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken);

    /// <summary>
    /// 首次登入時建立預設個人 Workspace。刻意不自行提交，由呼叫端與建立使用者放在同一次 SaveChanges。
    /// </summary>
    Task<Guid> CreateDefaultPersonalWorkspaceAsync(Guid userId, CancellationToken cancellationToken);
}
