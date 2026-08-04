using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;

namespace NestFlow_Backend.Services;

public class StorageItemService : IStorageItemService
{
    /// <summary>單次查詢的最大筆數，避免一次拉回過量資料。</summary>
    private const int MaxLimit = 500;

    private readonly IStorageItemRepository _itemRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public StorageItemService(
        IStorageItemRepository itemRepository,
        IWorkspaceService workspaceService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _itemRepository = itemRepository;
        _workspaceService = workspaceService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<StorageItemDtoModel> GetAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await GetAccessibleItemAsync(userId, itemId, cancellationToken);
        var owner = await _userRepository.GetByIdAsync(item.UserId, cancellationToken);

        return ToDto(item, owner?.DisplayName ?? string.Empty);
    }

    public async Task<StorageItemDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveStorageItemCommand command,
        CancellationToken cancellationToken)
    {
        // 前端傳入的 workspaceId 一律重新驗證 Membership
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);

        var now = _timeProvider.GetUtcNow();

        var item = new StorageItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            Name = command.Name,
            Location = command.Location,
            Note = command.Note,
            Status = StorageItemStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _itemRepository.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return ToDto(item, user?.DisplayName ?? string.Empty);
    }

    public async Task<StorageItemDtoModel> UpdateAsync(
        Guid userId,
        Guid itemId,
        Guid workspaceId,
        SaveStorageItemCommand command,
        CancellationToken cancellationToken)
    {
        var item = await GetAccessibleItemAsync(userId, itemId, cancellationToken);

        // 與記帳、代辦一致，第一版不支援把物品搬到其他資料空間
        if (item.WorkspaceId != workspaceId)
        {
            throw AppException.BadRequest("不能變更物品所屬的資料空間。");
        }

        item.Name = command.Name;
        item.Location = command.Location;
        item.Note = command.Note;

        // 更新時間會讓這筆浮到最近更新列表最上面
        item.UpdatedAt = _timeProvider.GetUtcNow();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var owner = await _userRepository.GetByIdAsync(item.UserId, cancellationToken);
        return ToDto(item, owner?.DisplayName ?? string.Empty);
    }

    public async Task DeleteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await GetAccessibleItemAsync(userId, itemId, cancellationToken);

        item.Status = StorageItemStatus.Deleted;
        item.UpdatedAt = _timeProvider.GetUtcNow();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<StorageItemDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        string? keyword,
        int? limit,
        CancellationToken cancellationToken)
    {
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);

        var items = await _itemRepository.ListAsync(
            workspaceId,
            string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim(),
            Math.Clamp(limit ?? MaxLimit, 1, MaxLimit),
            cancellationToken);

        return items
            .Select(x => ToDto(x, x.User?.DisplayName ?? string.Empty))
            .ToList();
    }

    /// <summary>
    /// 取得物品並確認呼叫者是該 Workspace 的有效成員。
    /// 家庭空間內任何成員都可修改與刪除共同物品紀錄，符合計畫第 7.4 節。
    /// </summary>
    private async Task<StorageItem> GetAccessibleItemAsync(
        Guid userId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var item = await _itemRepository.GetActiveAsync(itemId, cancellationToken)
            ?? throw AppException.NotFound();

        await _workspaceService.EnsureMemberAsync(userId, item.WorkspaceId, cancellationToken);

        return item;
    }

    private static StorageItemDtoModel ToDto(StorageItem item, string displayName) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Location = item.Location,
        Note = item.Note,
        UpdatedAt = item.UpdatedAt,
        CreatedByUserId = item.UserId,
        CreatedByDisplayName = displayName,
    };
}
