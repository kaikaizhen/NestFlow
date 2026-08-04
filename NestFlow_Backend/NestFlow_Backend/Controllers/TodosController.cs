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
/// PWA 代辦。一般代辦與購物清單共用同一組端點，以 type 區分。
/// 到期時間一律由前端依使用者時區換算為 UTC 後傳入，後端只認 UTC。
/// </summary>
[ApiController]
[Route("api/todos")]
[RequireSession]
public class TodosController : ControllerBase
{
    private readonly ITodoService _todoService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMapper _mapper;

    public TodosController(
        ITodoService todoService,
        ICurrentUserAccessor currentUser,
        IMapper mapper)
    {
        _todoService = todoService;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <summary>
    /// 取得指定類型的代辦。未指定 completed 時同時回傳未完成與已完成，
    /// 未完成排在前面，讓前端一次取得整個分頁需要的資料。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TodoViewModel>>> List(
        [FromQuery] Guid workspaceId,
        [FromQuery] string type,
        [FromQuery] bool? completed,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var dtos = await _todoService.ListAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            ParseType(type),
            completed,
            limit,
            cancellationToken);

        return Ok(_mapper.Map<List<TodoViewModel>>(dtos));
    }

    /// <summary>取得單筆代辦，供編輯畫面使用。</summary>
    [HttpGet("{todoId:guid}")]
    public async Task<ActionResult<TodoViewModel>> Get(Guid todoId, CancellationToken cancellationToken)
    {
        var dto = await _todoService.GetAsync(_currentUser.RequireUserId(), todoId, cancellationToken);

        return Ok(_mapper.Map<TodoViewModel>(dto));
    }

    [HttpPost]
    public async Task<ActionResult<TodoViewModel>> Create(
        [FromBody] SaveTodoParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _todoService.CreateAsync(
            _currentUser.RequireUserId(),
            param.WorkspaceId,
            ToCommand(param),
            cancellationToken);

        return Ok(_mapper.Map<TodoViewModel>(dto));
    }

    [HttpPut("{todoId:guid}")]
    public async Task<ActionResult<TodoViewModel>> Update(
        Guid todoId,
        [FromBody] SaveTodoParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _todoService.UpdateAsync(
            _currentUser.RequireUserId(),
            todoId,
            param.WorkspaceId,
            ToCommand(param),
            cancellationToken);

        return Ok(_mapper.Map<TodoViewModel>(dto));
    }

    /// <summary>標記完成或取消完成。獨立端點讓列表上的勾選不需送出整筆內容。</summary>
    [HttpPatch("{todoId:guid}/completion")]
    public async Task<ActionResult<TodoViewModel>> SetCompletion(
        Guid todoId,
        [FromBody] UpdateTodoCompletionParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _todoService.SetCompletionAsync(
            _currentUser.RequireUserId(),
            todoId,
            param.Completed,
            cancellationToken);

        return Ok(_mapper.Map<TodoViewModel>(dto));
    }

    [HttpDelete("{todoId:guid}")]
    public async Task<IActionResult> Delete(Guid todoId, CancellationToken cancellationToken)
    {
        await _todoService.DeleteAsync(_currentUser.RequireUserId(), todoId, cancellationToken);

        return NoContent();
    }

    /// <summary>把前端參數轉為已驗證與正規化的命令。</summary>
    private static SaveTodoCommand ToCommand(SaveTodoParamModel param)
    {
        var title = param.Title.Trim();

        // 只有空白字元時 StringLength 仍會通過，這裡再擋一次
        if (string.IsNullOrEmpty(title))
        {
            throw AppException.BadRequest("請輸入代辦內容。");
        }

        return new SaveTodoCommand(
            ParseType(param.Type),
            title,
            param.Quantity,
            param.DueAt?.ToUniversalTime());
    }

    private static TodoType ParseType(string? type)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            "general" => TodoType.General,
            "shopping" => TodoType.Shopping,
            _ => throw AppException.BadRequest("類型只能是 general 或 shopping。"),
        };
    }
}
