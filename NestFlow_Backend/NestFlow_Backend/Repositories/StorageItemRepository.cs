using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class StorageItemRepository : IStorageItemRepository
{
    private readonly NestFlowDbContext _dbContext;

    public StorageItemRepository(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(StorageItem item, CancellationToken cancellationToken)
    {
        await _dbContext.StorageItems.AddAsync(item, cancellationToken);
    }

    public Task<StorageItem?> GetActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.StorageItems.FirstOrDefaultAsync(
            x => x.Id == id && x.Status == StorageItemStatus.Active,
            cancellationToken);
    }

    public Task<List<StorageItem>> ListAsync(
        Guid workspaceId,
        string? keyword,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.StorageItems
            .Include(x => x.User)
            .Where(x => x.WorkspaceId == workspaceId && x.Status == StorageItemStatus.Active);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 只搜尋名稱與存放位置，符合計畫「依物品名稱及存放位置搜尋」
            query = query.Where(x => x.Name.Contains(keyword) || x.Location.Contains(keyword));
        }

        return query
            .OrderByDescending(x => x.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
