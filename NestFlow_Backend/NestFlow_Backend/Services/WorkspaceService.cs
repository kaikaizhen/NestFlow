using NestFlow_Backend.Common;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;

namespace NestFlow_Backend.Services;

public class WorkspaceService : IWorkspaceService
{
    /// <summary>邀請碼有效期，計畫第 7.3 節規定為 24 小時。</summary>
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(24);

    /// <summary>首次登入自動建立的個人 Workspace 名稱。</summary>
    private const string DefaultPersonalWorkspaceName = "個人";

    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICodeGenerator _codeGenerator;
    private readonly ICryptoHelper _cryptoHelper;
    private readonly TimeProvider _timeProvider;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ICodeGenerator codeGenerator,
        ICryptoHelper cryptoHelper,
        TimeProvider timeProvider)
    {
        _workspaceRepository = workspaceRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _cryptoHelper = cryptoHelper;
        _timeProvider = timeProvider;
    }

    public async Task<List<WorkspaceDtoModel>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized();

        var workspaces = await _workspaceRepository.ListForUserAsync(userId, cancellationToken);
        var result = new List<WorkspaceDtoModel>(workspaces.Count);

        foreach (var workspace in workspaces)
        {
            var membership = await _workspaceRepository.GetActiveMembershipAsync(workspace.Id, userId, cancellationToken);

            result.Add(ToDto(workspace, membership!.MembershipType, user.DefaultWorkspaceId == workspace.Id));
        }

        return result;
    }

    public async Task<WorkspaceDtoModel> CreateAsync(
        Guid userId,
        string name,
        WorkspaceType type,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized();

        var workspace = await CreateInternalAsync(userId, name, type, cancellationToken);

        // 使用者尚未設定預設資料空間時，第一個建立的 Workspace 直接成為預設
        if (user.DefaultWorkspaceId is null)
        {
            user.DefaultWorkspaceId = workspace.Id;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(workspace, MembershipType.Owner, user.DefaultWorkspaceId == workspace.Id);
    }

    public async Task<WorkspaceDtoModel> RenameAsync(
        Guid userId,
        Guid workspaceId,
        string name,
        CancellationToken cancellationToken)
    {
        var (workspace, _) = await GetAsOwnerAsync(userId, workspaceId, cancellationToken);

        workspace.Name = name;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return ToDto(workspace, MembershipType.Owner, user?.DefaultWorkspaceId == workspace.Id);
    }

    public async Task DeleteAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var (workspace, _) = await GetAsOwnerAsync(userId, workspaceId, cancellationToken);

        workspace.Status = WorkspaceStatus.Deleted;

        // 刪除 Workspace 時一併結束所有成員資格，成員立即失去存取權
        var members = await _workspaceRepository.ListMembersAsync(workspaceId, cancellationToken);
        foreach (var member in members)
        {
            member.Status = MembershipStatus.Removed;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvitationDtoModel> CreateInvitationAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var (workspace, _) = await GetAsOwnerAsync(userId, workspaceId, cancellationToken);

        if (workspace.Type != WorkspaceType.Family)
        {
            throw AppException.BadRequest("只有家庭資料空間可以產生邀請碼。");
        }

        var now = _timeProvider.GetUtcNow();
        var code = _codeGenerator.GenerateInvitationCode();

        await _workspaceRepository.AddInvitationAsync(
            new WorkspaceInvitation
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                CodeHash = _cryptoHelper.Hash(code),
                ExpiresAt = now.Add(InvitationLifetime),
                Status = InvitationStatus.Pending,
                CreatedByUserId = userId,
                CreatedAt = now,
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 明文只在此回傳一次，資料庫僅保存雜湊
        return new InvitationDtoModel { Code = code, ExpiresAt = now.Add(InvitationLifetime) };
    }

    public async Task<WorkspaceDtoModel> JoinAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized();

        var normalizedCode = code.Trim().ToUpperInvariant();
        var invitation = await _workspaceRepository.GetInvitationByCodeHashAsync(
            _cryptoHelper.Hash(normalizedCode),
            cancellationToken);

        // 邀請碼不存在、已使用或已過期，一律回傳相同訊息，避免用於探測
        if (invitation is null || invitation.Status != InvitationStatus.Pending || invitation.UsedAt is not null)
        {
            throw AppException.BadRequest("邀請碼無效或已被使用。");
        }

        var now = _timeProvider.GetUtcNow();
        if (invitation.ExpiresAt <= now)
        {
            invitation.Status = InvitationStatus.Expired;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw AppException.BadRequest("邀請碼無效或已被使用。");
        }

        var workspace = await _workspaceRepository.GetActiveAsync(invitation.WorkspaceId, cancellationToken)
            ?? throw AppException.BadRequest("邀請碼無效或已被使用。");

        var existing = await _workspaceRepository.GetActiveMembershipAsync(workspace.Id, userId, cancellationToken);
        if (existing is not null)
        {
            throw AppException.Conflict("你已經是這個資料空間的成員。");
        }

        await _workspaceRepository.AddMembershipAsync(
            new WorkspaceMembership
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                UserId = userId,
                MembershipType = MembershipType.Member,
                Status = MembershipStatus.Active,
                JoinedAt = now,
            },
            cancellationToken);

        // 使用成功後立即失效，確保一次性
        invitation.Status = InvitationStatus.Used;
        invitation.UsedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(workspace, MembershipType.Member, user.DefaultWorkspaceId == workspace.Id);
    }

    public async Task<List<WorkspaceMemberDtoModel>> ListMembersAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(userId, workspaceId, cancellationToken);

        var members = await _workspaceRepository.ListMembersAsync(workspaceId, cancellationToken);

        return members
            .Select(x => new WorkspaceMemberDtoModel
            {
                UserId = x.UserId,
                DisplayName = x.User?.DisplayName ?? string.Empty,
                PictureUrl = x.User?.PictureUrl,
                MembershipType = x.MembershipType,
                JoinedAt = x.JoinedAt,
            })
            .ToList();
    }

    public async Task RemoveMemberAsync(
        Guid actingUserId,
        Guid workspaceId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var actingMembership = await GetActiveMembershipOrNotFoundAsync(actingUserId, workspaceId, cancellationToken);

        var isSelf = actingUserId == targetUserId;
        var isOwner = actingMembership.MembershipType == MembershipType.Owner;

        if (!isSelf && !isOwner)
        {
            throw AppException.Forbidden("只有建立者可以移除其他成員。");
        }

        if (isSelf && isOwner)
        {
            throw AppException.BadRequest("建立者無法離開自己的資料空間，請改為刪除。");
        }

        var target = await _workspaceRepository.GetActiveMembershipAsync(workspaceId, targetUserId, cancellationToken)
            ?? throw AppException.NotFound("找不到這位成員。");

        if (target.MembershipType == MembershipType.Owner)
        {
            throw AppException.BadRequest("無法移除建立者。");
        }

        target.Status = MembershipStatus.Removed;

        // 被移除的成員若以此為預設資料空間，一併清除
        var targetUser = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser?.DefaultWorkspaceId == workspaceId)
        {
            targetUser.DefaultWorkspaceId = null;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDefaultAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(userId, workspaceId, cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized();

        user.DefaultWorkspaceId = workspaceId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureMemberAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken)
    {
        await GetActiveMembershipOrNotFoundAsync(userId, workspaceId, cancellationToken);
    }

    public async Task<Guid> CreateDefaultPersonalWorkspaceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var workspace = await CreateInternalAsync(
            userId,
            DefaultPersonalWorkspaceName,
            WorkspaceType.Personal,
            cancellationToken);

        return workspace.Id;
    }

    /// <summary>
    /// 建立 Workspace 與建立者成員資格。不自行 SaveChanges，由呼叫端決定交易邊界。
    /// </summary>
    private async Task<Workspace> CreateInternalAsync(
        Guid userId,
        string name,
        WorkspaceType type,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            Name = name,
            Type = type,
            Status = WorkspaceStatus.Active,
            CreatedAt = now,
        };

        await _workspaceRepository.AddAsync(workspace, cancellationToken);

        await _workspaceRepository.AddMembershipAsync(
            new WorkspaceMembership
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                UserId = userId,
                MembershipType = MembershipType.Owner,
                Status = MembershipStatus.Active,
                JoinedAt = now,
            },
            cancellationToken);

        return workspace;
    }

    private async Task<(Workspace Workspace, WorkspaceMembership Membership)> GetAsOwnerAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var membership = await GetActiveMembershipOrNotFoundAsync(userId, workspaceId, cancellationToken);

        if (membership.MembershipType != MembershipType.Owner)
        {
            throw AppException.Forbidden("只有建立者可以執行此操作。");
        }

        var workspace = await _workspaceRepository.GetActiveAsync(workspaceId, cancellationToken)
            ?? throw AppException.NotFound();

        return (workspace, membership);
    }

    private async Task<WorkspaceMembership> GetActiveMembershipOrNotFoundAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetActiveAsync(workspaceId, cancellationToken);

        // 非成員與不存在一律回傳 404，避免洩漏 Workspace 是否存在
        if (workspace is null)
        {
            throw AppException.NotFound();
        }

        return await _workspaceRepository.GetActiveMembershipAsync(workspaceId, userId, cancellationToken)
            ?? throw AppException.NotFound();
    }

    private static WorkspaceDtoModel ToDto(Workspace workspace, MembershipType membershipType, bool isDefault)
    {
        return new WorkspaceDtoModel
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Type = workspace.Type,
            MembershipType = membershipType,
            IsDefault = isDefault,
            CreatedAt = workspace.CreatedAt,
        };
    }
}
