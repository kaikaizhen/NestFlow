using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class AccountEntryRepository : IAccountEntryRepository
{
    private readonly NestFlowDbContext _dbContext;

    public AccountEntryRepository(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AccountEntry entry, CancellationToken cancellationToken)
    {
        await _dbContext.AccountEntries.AddAsync(entry, cancellationToken);
    }

    public Task<AccountEntry?> GetActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.AccountEntries.Include(x => x.Shares).FirstOrDefaultAsync(
            x => x.Id == id && x.Status == EntryStatus.Active,
            cancellationToken);
    }

    public Task<List<AccountEntry>> ListAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery(workspaceId, fromUtc, toUtc)
            .Include(x => x.User)
            .Include(x => x.Shares)
                .ThenInclude(x => x.User)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt);

        return limit is > 0
            ? query.Take(limit.Value).ToListAsync(cancellationToken)
            : query.ToListAsync(cancellationToken);
    }

    public Task<List<AccountEntry>> ListUnsettledSharedAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        return BaseQuery(workspaceId, fromUtc, toUtc)
            .Where(x => x.Type == EntryType.Expense && x.SettledAt == null && x.Shares.Any())
            .Include(x => x.User)
            .Include(x => x.Shares)
                .ThenInclude(x => x.User)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CurrencyTotal>> SummarizeAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        // 篩選交給資料庫（有索引），加總在記憶體完成。
        // decimal 的 SQL 端 Sum 在不同資料庫提供者行為不一致，改為只取必要欄位後自行加總，
        // 可確保金額精度完全一致。查詢區間上限為 400 天，資料量可控。
        var rows = await BaseQuery(workspaceId, fromUtc, toUtc)
            .Select(x => new { x.Currency, x.Type, x.Amount })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.Currency)
            .Select(g => new CurrencyTotal(
                g.Key,
                g.Where(x => x.Type == EntryType.Income).Sum(x => x.Amount),
                g.Where(x => x.Type == EntryType.Expense).Sum(x => x.Amount)))
            .OrderByDescending(x => x.Income + x.Expense)
            .ToList();
    }

    /// <summary>共用篩選：只取指定 Workspace 中未刪除且落在區間內的記帳。</summary>
    private IQueryable<AccountEntry> BaseQuery(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        return _dbContext.AccountEntries.Where(x =>
            x.WorkspaceId == workspaceId
            && x.Status == EntryStatus.Active
            && x.OccurredAt >= fromUtc
            && x.OccurredAt < toUtc);
    }
}
