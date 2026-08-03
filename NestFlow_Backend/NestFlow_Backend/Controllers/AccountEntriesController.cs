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
/// PWA 記帳。時間區間一律由前端依使用者時區換算為 UTC 後傳入，後端只認 UTC。
/// </summary>
[ApiController]
[Route("api/account-entries")]
[RequireSession]
public class AccountEntriesController : ControllerBase
{
    private readonly IAccountEntryService _entryService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMapper _mapper;

    public AccountEntriesController(
        IAccountEntryService entryService,
        ICurrentUserAccessor currentUser,
        IMapper mapper)
    {
        _entryService = entryService;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <summary>取得固定分類清單，供前端顯示名稱與圖示。</summary>
    [HttpGet("categories")]
    public ActionResult<List<CategoryViewModel>> Categories()
    {
        return Ok(AccountCategories.All
            .Select(x => new CategoryViewModel
            {
                Code = x.Code,
                Label = x.Label,
                Type = x.Type.ToString().ToLowerInvariant(),
            })
            .ToList());
    }

    /// <summary>取得區間內的記帳，依發生時間新到舊排序。</summary>
    [HttpGet]
    public async Task<ActionResult<List<AccountEntryViewModel>>> List(
        [FromQuery] Guid workspaceId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var dtos = await _entryService.ListAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            from.ToUniversalTime(),
            to.ToUniversalTime(),
            limit,
            cancellationToken);

        return Ok(_mapper.Map<List<AccountEntryViewModel>>(dtos));
    }

    /// <summary>取得區間內的收入、支出與結餘，依幣別分開加總。</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<CurrencySummaryViewModel>>> Summary(
        [FromQuery] Guid workspaceId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var dtos = await _entryService.SummarizeAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            from.ToUniversalTime(),
            to.ToUniversalTime(),
            cancellationToken);

        return Ok(_mapper.Map<List<CurrencySummaryViewModel>>(dtos));
    }

    [HttpPost]
    public async Task<ActionResult<AccountEntryViewModel>> Create(
        [FromBody] SaveAccountEntryParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _entryService.CreateAsync(
            _currentUser.RequireUserId(),
            param.WorkspaceId,
            ToCommand(param),
            cancellationToken);

        return Ok(_mapper.Map<AccountEntryViewModel>(dto));
    }

    [HttpPut("{entryId:guid}")]
    public async Task<ActionResult<AccountEntryViewModel>> Update(
        Guid entryId,
        [FromBody] SaveAccountEntryParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _entryService.UpdateAsync(
            _currentUser.RequireUserId(),
            entryId,
            param.WorkspaceId,
            ToCommand(param),
            cancellationToken);

        return Ok(_mapper.Map<AccountEntryViewModel>(dto));
    }

    [HttpDelete("{entryId:guid}")]
    public async Task<IActionResult> Delete(Guid entryId, CancellationToken cancellationToken)
    {
        await _entryService.DeleteAsync(_currentUser.RequireUserId(), entryId, cancellationToken);

        return NoContent();
    }

    /// <summary>把前端參數轉為已驗證與正規化的命令。</summary>
    private static SaveAccountEntryCommand ToCommand(SaveAccountEntryParamModel param)
    {
        var type = param.Type.Trim().ToLowerInvariant() switch
        {
            "expense" => EntryType.Expense,
            "income" => EntryType.Income,
            _ => throw AppException.BadRequest("類型只能是 expense 或 income。"),
        };

        var note = string.IsNullOrWhiteSpace(param.Note) ? null : param.Note.Trim();

        return new SaveAccountEntryCommand(
            type,
            decimal.Round(param.Amount, 2, MidpointRounding.AwayFromZero),
            SupportedCurrencies.Normalize(param.Currency),
            AccountCategories.Normalize(param.Category, type),
            note,
            param.OccurredAt.ToUniversalTime());
    }
}
