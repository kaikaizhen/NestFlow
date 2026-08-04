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
/// PWA 行程。時間區間一律由前端依使用者時區換算為 UTC 後傳入，後端只認 UTC。
/// </summary>
[ApiController]
[Route("api/calendar-events")]
[RequireSession]
public class CalendarEventsController : ControllerBase
{
    private readonly ICalendarEventService _eventService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMapper _mapper;

    public CalendarEventsController(
        ICalendarEventService eventService,
        ICurrentUserAccessor currentUser,
        IMapper mapper)
    {
        _eventService = eventService;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <summary>
    /// 取得與區間有重疊的行程，依開始時間由早到晚排序。
    /// 月曆、今日與本週、即將到來都由前端指定不同區間取得。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CalendarEventViewModel>>> List(
        [FromQuery] Guid workspaceId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var dtos = await _eventService.ListAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            from.ToUniversalTime(),
            to.ToUniversalTime(),
            limit,
            cancellationToken);

        return Ok(_mapper.Map<List<CalendarEventViewModel>>(dtos));
    }

    /// <summary>取得單筆行程，供編輯畫面使用。</summary>
    [HttpGet("{eventId:guid}")]
    public async Task<ActionResult<CalendarEventViewModel>> Get(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var dto = await _eventService.GetAsync(_currentUser.RequireUserId(), eventId, cancellationToken);

        return Ok(_mapper.Map<CalendarEventViewModel>(dto));
    }

    [HttpPost]
    public async Task<ActionResult<CalendarEventViewModel>> Create(
        [FromBody] SaveCalendarEventParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _eventService.CreateAsync(
            _currentUser.RequireUserId(),
            param.WorkspaceId,
            ToCommand(param),
            cancellationToken);

        return Ok(_mapper.Map<CalendarEventViewModel>(dto));
    }

    [HttpPut("{eventId:guid}")]
    public async Task<ActionResult<CalendarEventViewModel>> Update(
        Guid eventId,
        [FromBody] SaveCalendarEventParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _eventService.UpdateAsync(
            _currentUser.RequireUserId(),
            eventId,
            param.WorkspaceId,
            ToCommand(param),
            cancellationToken);

        return Ok(_mapper.Map<CalendarEventViewModel>(dto));
    }

    /// <summary>刪除單一場次（週期行程即為「僅此次取消」）。</summary>
    [HttpDelete("{eventId:guid}")]
    public async Task<IActionResult> Delete(Guid eventId, CancellationToken cancellationToken)
    {
        await _eventService.DeleteAsync(_currentUser.RequireUserId(), eventId, cancellationToken);

        return NoContent();
    }

    /// <summary>刪除整個週期系列中尚未發生的場次。非週期行程效果等同單筆刪除。</summary>
    [HttpDelete("{eventId:guid}/series")]
    public async Task<IActionResult> DeleteSeries(Guid eventId, CancellationToken cancellationToken)
    {
        await _eventService.DeleteSeriesAsync(_currentUser.RequireUserId(), eventId, cancellationToken);

        return NoContent();
    }

    /// <summary>把前端參數轉為已正規化的命令。</summary>
    private static SaveCalendarEventCommand ToCommand(SaveCalendarEventParamModel param)
    {
        var description = string.IsNullOrWhiteSpace(param.Description) ? null : param.Description.Trim();

        return new SaveCalendarEventCommand(
            param.Title.Trim(),
            description,
            param.StartAt.ToUniversalTime(),
            param.EndAt.ToUniversalTime(),
            param.WantsReminder,
            param.ReminderMinutesBeforeStart,
            param.Repeat ? ParseRecurrenceEndType(param.RepeatEndType) : null,
            param.RepeatCount,
            param.RepeatUntil?.ToUniversalTime());
    }

    private static CalendarRecurrenceEndType ParseRecurrenceEndType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "count" => CalendarRecurrenceEndType.Count,
            "until" => CalendarRecurrenceEndType.UntilDate,
            "forever" => CalendarRecurrenceEndType.Forever,
            _ => throw AppException.BadRequest("重複結束方式只能是 count、until 或 forever。"),
        };
    }
}
