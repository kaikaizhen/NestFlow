using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using NestFlow_Backend.Common;
using NestFlow_Backend.Filters;
using NestFlow_Backend.Models.ParamModels;
using NestFlow_Backend.Models.ViewModels;
using NestFlow_Backend.Services;

namespace NestFlow_Backend.Controllers;

[ApiController]
[Route("api/workspaces")]
[RequireSession]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMapper _mapper;

    public WorkspacesController(
        IWorkspaceService workspaceService,
        ICurrentUserAccessor currentUser,
        IMapper mapper)
    {
        _workspaceService = workspaceService;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <summary>列出目前使用者可存取的所有資料空間。</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkspaceViewModel>>> List(CancellationToken cancellationToken)
    {
        var dtos = await _workspaceService.ListAsync(_currentUser.RequireUserId(), cancellationToken);

        return Ok(_mapper.Map<List<WorkspaceViewModel>>(dtos));
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceViewModel>> Create(
        [FromBody] CreateWorkspaceParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _workspaceService.CreateAsync(
            _currentUser.RequireUserId(),
            param.Name.Trim(),
            ParseType(param.Type),
            cancellationToken);

        return Ok(_mapper.Map<WorkspaceViewModel>(dto));
    }

    [HttpPut("{workspaceId:guid}")]
    public async Task<ActionResult<WorkspaceViewModel>> Rename(
        Guid workspaceId,
        [FromBody] RenameWorkspaceParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _workspaceService.RenameAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            param.Name.Trim(),
            cancellationToken);

        return Ok(_mapper.Map<WorkspaceViewModel>(dto));
    }

    [HttpDelete("{workspaceId:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, CancellationToken cancellationToken)
    {
        await _workspaceService.DeleteAsync(_currentUser.RequireUserId(), workspaceId, cancellationToken);

        return NoContent();
    }

    /// <summary>產生 24 小時一次性邀請碼。明文只在此回傳一次。</summary>
    [HttpPost("{workspaceId:guid}/invitations")]
    public async Task<ActionResult<InvitationViewModel>> CreateInvitation(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var dto = await _workspaceService.CreateInvitationAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            cancellationToken);

        return Ok(_mapper.Map<InvitationViewModel>(dto));
    }

    [HttpPost("join")]
    public async Task<ActionResult<WorkspaceViewModel>> Join(
        [FromBody] JoinWorkspaceParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _workspaceService.JoinAsync(_currentUser.RequireUserId(), param.Code, cancellationToken);

        return Ok(_mapper.Map<WorkspaceViewModel>(dto));
    }

    [HttpGet("{workspaceId:guid}/members")]
    public async Task<ActionResult<List<WorkspaceMemberViewModel>>> ListMembers(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var dtos = await _workspaceService.ListMembersAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            cancellationToken);

        return Ok(_mapper.Map<List<WorkspaceMemberViewModel>>(dtos));
    }

    /// <summary>建立者移除成員；成員傳入自己的 Id 即為主動離開。</summary>
    [HttpDelete("{workspaceId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _workspaceService.RemoveMemberAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            userId,
            cancellationToken);

        return NoContent();
    }

    /// <summary>設定 LINE 訊息預設寫入的資料空間。</summary>
    [HttpPut("default")]
    public async Task<IActionResult> SetDefault(
        [FromBody] SetDefaultWorkspaceParamModel param,
        CancellationToken cancellationToken)
    {
        await _workspaceService.SetDefaultAsync(
            _currentUser.RequireUserId(),
            param.WorkspaceId,
            cancellationToken);

        return NoContent();
    }

    private static WorkspaceType ParseType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "personal" => WorkspaceType.Personal,
            "family" => WorkspaceType.Family,
            _ => throw AppException.BadRequest("類型只能是 personal 或 family。"),
        };
    }
}
