using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly NestFlowDbContext _dbContext;

    public WorkspaceRepository(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        await _dbContext.Workspaces.AddAsync(workspace, cancellationToken);
    }

    public Task<Workspace?> GetActiveAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return _dbContext.Workspaces.FirstOrDefaultAsync(
            x => x.Id == workspaceId && x.Status == WorkspaceStatus.Active,
            cancellationToken);
    }

    public Task<List<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.WorkspaceMemberships
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Join(
                _dbContext.Workspaces.Where(w => w.Status == WorkspaceStatus.Active),
                m => m.WorkspaceId,
                w => w.Id,
                (_, w) => w)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddMembershipAsync(WorkspaceMembership membership, CancellationToken cancellationToken)
    {
        await _dbContext.WorkspaceMemberships.AddAsync(membership, cancellationToken);
    }

    public Task<WorkspaceMembership?> GetActiveMembershipAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _dbContext.WorkspaceMemberships.FirstOrDefaultAsync(
            x => x.WorkspaceId == workspaceId && x.UserId == userId && x.Status == MembershipStatus.Active,
            cancellationToken);
    }

    public Task<List<WorkspaceMembership>> ListMembersAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return _dbContext.WorkspaceMemberships
            .Include(x => x.User)
            .Where(x => x.WorkspaceId == workspaceId && x.Status == MembershipStatus.Active)
            .OrderBy(x => x.JoinedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddInvitationAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken)
    {
        await _dbContext.WorkspaceInvitations.AddAsync(invitation, cancellationToken);
    }

    public Task<WorkspaceInvitation?> GetInvitationByCodeHashAsync(string codeHash, CancellationToken cancellationToken)
    {
        return _dbContext.WorkspaceInvitations.FirstOrDefaultAsync(
            x => x.CodeHash == codeHash,
            cancellationToken);
    }
}
