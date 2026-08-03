using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using NestFlow_Backend.Common;
using NestFlow_Backend.Filters;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.ParamModels;
using NestFlow_Backend.Models.ViewModels;
using NestFlow_Backend.Services;

namespace NestFlow_Backend.Controllers;

/// <summary>
/// PWA 提醒。時間區間一律由前端依使用者時區換算為 UTC 後傳入，後端只認 UTC。
/// </summary>
[ApiController]
[Route("api/reminders")]
[RequireSession]
public class RemindersController : ControllerBase
{
    private readonly IReminderService _reminderService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMapper _mapper;

    public RemindersController(
        IReminderService reminderService,
        ICurrentUserAccessor currentUser,
        IMapper mapper)
    {
        _reminderService = reminderService;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <summary>取得區間內的提醒，依觸發時間由早到晚排序。已取消的不列出。</summary>
    [HttpGet]
    public async Task<ActionResult<List<ReminderViewModel>>> List(
        [FromQuery] Guid workspaceId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var dtos = await _reminderService.ListAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            from.ToUniversalTime(),
            to.ToUniversalTime(),
            limit,
            cancellationToken);

        return Ok(_mapper.Map<List<ReminderViewModel>>(dtos));
    }

    [HttpPost]
    public async Task<ActionResult<ReminderViewModel>> Create(
        [FromBody] SaveReminderParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _reminderService.CreateAsync(
            _currentUser.RequireUserId(),
            param.WorkspaceId,
            new SaveReminderCommand(param.Content.Trim(), param.TriggerAt.ToUniversalTime()),
            cancellationToken);

        return Ok(_mapper.Map<ReminderViewModel>(dto));
    }

    /// <summary>取消提醒。已發送的提醒不可取消。</summary>
    [HttpDelete("{reminderId:guid}")]
    public async Task<IActionResult> Cancel(Guid reminderId, CancellationToken cancellationToken)
    {
        await _reminderService.CancelAsync(_currentUser.RequireUserId(), reminderId, cancellationToken);

        return NoContent();
    }
}
