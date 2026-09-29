using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

/// <summary>各幣別的收支加總結果。第一版不做匯率換算，因此分幣別回傳。</summary>
public record CurrencyTotal(string Currency, decimal Income, decimal Expense);

public interface IAccountEntryRepository
{
    Task AddAsync(AccountEntry entry, CancellationToken cancellationToken);

    /// <summary>取得未刪除的單筆記帳。</summary>
    Task<AccountEntry?> GetActiveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 依 Workspace 與 UTC 時間區間取得記帳，依發生時間新到舊排序。
    /// 區間為前閉後開，由前端依使用者時區換算後傳入。
    /// </summary>
    Task<List<AccountEntry>> ListAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<List<AccountEntry>> ListUnsettledSharedAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    /// <summary>依幣別加總區間內的收入與支出。</summary>
    Task<List<CurrencyTotal>> SummarizeAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}
