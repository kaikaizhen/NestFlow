using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;

namespace NestFlow_Backend.Services;

public class AccountEntryService : IAccountEntryService
{
    /// <summary>單次查詢的最大筆數，避免一次拉回過量資料。</summary>
    private const int MaxLimit = 500;

    private readonly IAccountEntryRepository _entryRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AccountEntryService(
        IAccountEntryRepository entryRepository,
        IWorkspaceService workspaceService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _entryRepository = entryRepository;
        _workspaceService = workspaceService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<AccountEntryDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveAccountEntryCommand command,
        CancellationToken cancellationToken)
    {
        // 前端傳入的 workspaceId 一律重新驗證 Membership
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);

        var entry = new AccountEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            Type = command.Type,
            Amount = command.Amount,
            Currency = command.Currency,
            Category = command.Category,
            Note = command.Note,
            OccurredAt = command.OccurredAtUtc,
            Status = EntryStatus.Active,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        await _entryRepository.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return ToDto(entry, user?.DisplayName ?? string.Empty);
    }

    public async Task<AccountEntryDtoModel> UpdateAsync(
        Guid userId,
        Guid entryId,
        Guid workspaceId,
        SaveAccountEntryCommand command,
        CancellationToken cancellationToken)
    {
        var entry = await GetAccessibleEntryAsync(userId, entryId, cancellationToken);

        // 第一版不支援把記帳搬到其他資料空間
        if (entry.WorkspaceId != workspaceId)
        {
            throw AppException.BadRequest("不能變更記帳所屬的資料空間。");
        }

        entry.Type = command.Type;
        entry.Amount = command.Amount;
        entry.Currency = command.Currency;
        entry.Category = command.Category;
        entry.Note = command.Note;
        entry.OccurredAt = command.OccurredAtUtc;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var owner = await _userRepository.GetByIdAsync(entry.UserId, cancellationToken);
        return ToDto(entry, owner?.DisplayName ?? string.Empty);
    }

    public async Task DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await GetAccessibleEntryAsync(userId, entryId, cancellationToken);

        entry.Status = EntryStatus.Deleted;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AccountEntryDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);
        EnsureValidRange(fromUtc, toUtc);

        var entries = await _entryRepository.ListAsync(
            workspaceId,
            fromUtc,
            toUtc,
            Math.Min(limit ?? MaxLimit, MaxLimit),
            cancellationToken);

        return entries
            .Select(x => ToDto(x, x.User?.DisplayName ?? string.Empty))
            .ToList();
    }

    public async Task<List<CurrencySummaryDtoModel>> SummarizeAsync(
        Guid userId,
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);
        EnsureValidRange(fromUtc, toUtc);

        var totals = await _entryRepository.SummarizeAsync(workspaceId, fromUtc, toUtc, cancellationToken);

        // 即使區間內沒有任何記帳，也回傳一筆預設幣別的零值，讓前端版面穩定
        if (totals.Count == 0)
        {
            return
            [
                new CurrencySummaryDtoModel
                {
                    Currency = SupportedCurrencies.Default,
                    Income = 0,
                    Expense = 0,
                    Balance = 0,
                },
            ];
        }

        return totals
            .Select(x => new CurrencySummaryDtoModel
            {
                Currency = x.Currency,
                Income = x.Income,
                Expense = x.Expense,
                Balance = x.Income - x.Expense,
            })
            .ToList();
    }

    /// <summary>
    /// 取得記帳並確認呼叫者是該 Workspace 的有效成員。
    /// 家庭空間內任何成員都可修改與刪除共同記帳，符合計畫第 7.4 節。
    /// </summary>
    private async Task<AccountEntry> GetAccessibleEntryAsync(
        Guid userId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        var entry = await _entryRepository.GetActiveAsync(entryId, cancellationToken)
            ?? throw AppException.NotFound();

        await _workspaceService.EnsureMemberAsync(userId, entry.WorkspaceId, cancellationToken);

        return entry;
    }

    private static void EnsureValidRange(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (toUtc <= fromUtc)
        {
            throw AppException.BadRequest("結束時間必須晚於開始時間。");
        }

        if (toUtc - fromUtc > TimeSpan.FromDays(400))
        {
            throw AppException.BadRequest("查詢區間不可超過 400 天。");
        }
    }

    private static AccountEntryDtoModel ToDto(AccountEntry entry, string displayName) => new()
    {
        Id = entry.Id,
        Type = entry.Type,
        Amount = entry.Amount,
        Currency = entry.Currency,
        Category = entry.Category,
        Note = entry.Note,
        OccurredAt = entry.OccurredAt,
        CreatedByUserId = entry.UserId,
        CreatedByDisplayName = displayName,
    };
}
