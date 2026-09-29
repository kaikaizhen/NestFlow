using NestFlow_Backend.Models.Dtos;

namespace NestFlow_Backend.Services;

public interface IAccountEntryService
{
    /// <summary>取得單筆記帳，供編輯畫面使用。</summary>
    Task<AccountEntryDtoModel> GetAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);

    Task<AccountEntryDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveAccountEntryCommand command,
        CancellationToken cancellationToken);

    Task<AccountEntryDtoModel> UpdateAsync(
        Guid userId,
        Guid entryId,
        Guid workspaceId,
        SaveAccountEntryCommand command,
        CancellationToken cancellationToken);

    /// <summary>軟刪除，資料保留於資料庫但不再計入列表與統計。</summary>
    Task DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);

    Task<List<AccountEntryDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<List<CurrencySummaryDtoModel>> SummarizeAsync(
        Guid userId,
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<SettlementSummaryDtoModel> GetSettlementAsync(
        Guid userId, Guid workspaceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    Task CloseSettlementAsync(
        Guid userId, Guid workspaceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}
