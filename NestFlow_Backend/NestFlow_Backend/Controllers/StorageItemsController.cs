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
/// PWA 儲藏庫。記錄東西放在哪裡，並可依物品名稱或存放位置搜尋。
/// </summary>
[ApiController]
[Route("api/storage-items")]
[RequireSession]
public class StorageItemsController : ControllerBase
{
    private readonly IStorageItemService _itemService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMapper _mapper;

    public StorageItemsController(
        IStorageItemService itemService,
        ICurrentUserAccessor currentUser,
        IMapper mapper)
    {
        _itemService = itemService;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    /// <summary>
    /// 取得最近更新的物品。帶 keyword 時改為依物品名稱與存放位置搜尋，備註不列入。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<StorageItemViewModel>>> List(
        [FromQuery] Guid workspaceId,
        [FromQuery] string? keyword,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var dtos = await _itemService.ListAsync(
            _currentUser.RequireUserId(),
            workspaceId,
            keyword,
            limit,
            cancellationToken);

        return Ok(_mapper.Map<List<StorageItemViewModel>>(dtos));
    }

    /// <summary>取得單筆物品，供編輯畫面使用。</summary>
    [HttpGet("{itemId:guid}")]
    public async Task<ActionResult<StorageItemViewModel>> Get(Guid itemId, CancellationToken cancellationToken)
    {
        var dto = await _itemService.GetAsync(_currentUser.RequireUserId(), itemId, cancellationToken);

        return Ok(_mapper.Map<StorageItemViewModel>(dto));
    }

    [HttpPost]
    public async Task<ActionResult<StorageItemViewModel>> Create(
        [FromBody] SaveStorageItemParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _itemService.CreateAsync(
            _currentUser.RequireUserId(),
            param.WorkspaceId,
            ToCommand(param),
            cancellationToken);

        return Ok(_mapper.Map<StorageItemViewModel>(dto));
    }

    [HttpPut("{itemId:guid}")]
    public async Task<ActionResult<StorageItemViewModel>> Update(
        Guid itemId,
        [FromBody] SaveStorageItemParamModel param,
        CancellationToken cancellationToken)
    {
        var dto = await _itemService.UpdateAsync(
            _currentUser.RequireUserId(),
            itemId,
            param.WorkspaceId,
            ToCommand(param),
            cancellationToken);

        return Ok(_mapper.Map<StorageItemViewModel>(dto));
    }

    [HttpDelete("{itemId:guid}")]
    public async Task<IActionResult> Delete(Guid itemId, CancellationToken cancellationToken)
    {
        await _itemService.DeleteAsync(_currentUser.RequireUserId(), itemId, cancellationToken);

        return NoContent();
    }

    /// <summary>把前端參數轉為已驗證與正規化的命令。</summary>
    private static SaveStorageItemCommand ToCommand(SaveStorageItemParamModel param)
    {
        var name = param.Name.Trim();
        var location = param.Location.Trim();

        // 只有空白字元時 StringLength 仍會通過，這裡再擋一次
        if (string.IsNullOrEmpty(name))
        {
            throw AppException.BadRequest("請輸入物品名稱。");
        }

        if (string.IsNullOrEmpty(location))
        {
            throw AppException.BadRequest("請輸入存放位置。");
        }

        var note = string.IsNullOrWhiteSpace(param.Note) ? null : param.Note.Trim();

        return new SaveStorageItemCommand(name, location, note);
    }
}
