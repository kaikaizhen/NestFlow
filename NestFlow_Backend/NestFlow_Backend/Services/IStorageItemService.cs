using NestFlow_Backend.Models.Dtos;

namespace NestFlow_Backend.Services;

public interface IStorageItemService
{
    /// <summary>取得單筆物品，供編輯畫面使用。</summary>
    Task<StorageItemDtoModel> GetAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);

    Task<StorageItemDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveStorageItemCommand command,
        CancellationToken cancellationToken);

    Task<StorageItemDtoModel> UpdateAsync(
        Guid userId,
        Guid itemId,
        Guid workspaceId,
        SaveStorageItemCommand command,
        CancellationToken cancellationToken);

    /// <summary>軟刪除，資料保留於資料庫但不再出現於列表與搜尋。</summary>
    Task DeleteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);

    /// <summary>
    /// 取得最近更新的物品。<paramref name="keyword"/> 有值時改為依名稱與存放位置搜尋。
    /// </summary>
    Task<List<StorageItemDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        string? keyword,
        int? limit,
        CancellationToken cancellationToken);
}
