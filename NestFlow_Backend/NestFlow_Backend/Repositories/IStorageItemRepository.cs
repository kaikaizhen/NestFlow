using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public interface IStorageItemRepository
{
    Task AddAsync(StorageItem item, CancellationToken cancellationToken);

    /// <summary>取得未刪除的單筆物品。</summary>
    Task<StorageItem?> GetActiveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 依 Workspace 取得未刪除的物品，依最後更新時間由新到舊排序。
    /// <paramref name="keyword"/> 有值時只比對物品名稱與存放位置，備註不列入搜尋。
    /// </summary>
    Task<List<StorageItem>> ListAsync(
        Guid workspaceId,
        string? keyword,
        int limit,
        CancellationToken cancellationToken);
}
