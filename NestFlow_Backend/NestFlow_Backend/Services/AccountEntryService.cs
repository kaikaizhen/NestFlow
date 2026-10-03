using Microsoft.EntityFrameworkCore;
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
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AccountEntryService(
        IAccountEntryRepository entryRepository,
        IWorkspaceService workspaceService,
        IWorkspaceRepository workspaceRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _entryRepository = entryRepository;
        _workspaceService = workspaceService;
        _workspaceRepository = workspaceRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<AccountEntryDtoModel> GetAsync(
        Guid userId,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        var entry = await GetAccessibleEntryAsync(userId, entryId, cancellationToken);
        var owner = await _userRepository.GetByIdAsync(entry.UserId, cancellationToken);

        return ToDto(entry, owner?.DisplayName ?? string.Empty);
    }

    public async Task<AccountEntryDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveAccountEntryCommand command,
        CancellationToken cancellationToken)
    {
        // 前端傳入的 workspaceId 一律重新驗證 Membership
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);
        var shares = await ValidateAndBuildSharesAsync(userId, workspaceId, command, cancellationToken);

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
            Shares = shares,
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

        if (entry.SettledAt != null)
        {
            throw AppException.BadRequest("此筆分攤已結清，無法修改；如需調整請新增一筆更正帳目。");
        }

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
        entry.Shares.Clear();
        var shares = await ValidateAndBuildSharesAsync(userId, workspaceId, command, cancellationToken);
        foreach (var share in shares)
        {
            entry.Shares.Add(share);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw AppException.Conflict("這筆記帳資料已被其他人修改或刪除，請重新整理後再試。");
        }

        var owner = await _userRepository.GetByIdAsync(entry.UserId, cancellationToken);
        return ToDto(entry, owner?.DisplayName ?? string.Empty);
    }

    public async Task DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await GetAccessibleEntryAsync(userId, entryId, cancellationToken);

        if (entry.SettledAt != null)
        {
            throw AppException.BadRequest("此筆分攤已結清，無法刪除；如需調整請新增一筆更正帳目。");
        }

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

    public async Task<SettlementSummaryDtoModel> GetSettlementAsync(
        Guid userId, Guid workspaceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);
        EnsureValidRange(fromUtc, toUtc);
        var entries = await _entryRepository.ListUnsettledSharedAsync(workspaceId, fromUtc, toUtc, cancellationToken);

        var balances = new Dictionary<(string Currency, string Party), decimal>();
        var names = new Dictionary<string, string>();
        void Add(string currency, string party, string name, decimal amount)
        {
            var key = (currency, party);
            balances[key] = balances.GetValueOrDefault(key) + amount;
            names[party] = name;
        }

        foreach (var entry in entries)
        {
            var payer = $"user:{entry.UserId}";
            Add(entry.Currency, payer, entry.User?.DisplayName ?? "付款人", entry.Amount);
            foreach (var share in entry.Shares)
            {
                var party = share.UserId is Guid memberId ? $"user:{memberId}" : $"guest:{share.ParticipantName.Trim().ToUpperInvariant()}";
                Add(entry.Currency, party, share.User?.DisplayName ?? share.ParticipantName, -share.Amount);
            }
        }

        var result = new SettlementSummaryDtoModel();
        foreach (var currencyGroup in balances.GroupBy(x => x.Key.Currency))
        {
            var debtors = currencyGroup.Where(x => x.Value < 0).Select(x => (Party: x.Key.Party, Amount: -x.Value)).ToList();
            var creditors = currencyGroup.Where(x => x.Value > 0).Select(x => (Party: x.Key.Party, Amount: x.Value)).ToList();
            var debtorIndex = 0;
            var creditorIndex = 0;
            while (debtorIndex < debtors.Count && creditorIndex < creditors.Count)
            {
                var amount = decimal.Min(debtors[debtorIndex].Amount, creditors[creditorIndex].Amount);
                var from = debtors[debtorIndex].Party;
                var to = creditors[creditorIndex].Party;
                if (from == $"user:{userId}") result.ToPay.Add(Transfer(names[to], currencyGroup.Key, amount));
                if (to == $"user:{userId}") result.ToReceive.Add(Transfer(names[from], currencyGroup.Key, amount));
                debtors[debtorIndex] = (from, debtors[debtorIndex].Amount - amount);
                creditors[creditorIndex] = (to, creditors[creditorIndex].Amount - amount);
                if (debtors[debtorIndex].Amount == 0) debtorIndex++;
                if (creditors[creditorIndex].Amount == 0) creditorIndex++;
            }
        }
        return result;
    }

    public async Task CloseSettlementAsync(
        Guid userId, Guid workspaceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);
        EnsureValidRange(fromUtc, toUtc);
        var entries = await _entryRepository.ListUnsettledSharedAsync(workspaceId, fromUtc, toUtc, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        foreach (var entry in entries)
        {
            entry.SettledAt = now;
            entry.SettledByUserId = userId;
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        PaymentMode = entry.Shares.Count == 0 ? "full" : "split",
        IsSettled = entry.SettledAt != null,
        Shares = entry.Shares.Select(x => new AccountEntryShareDtoModel
        {
            UserId = x.UserId,
            ParticipantName = x.User?.DisplayName ?? x.ParticipantName,
            Amount = x.Amount,
        }).ToList(),
    };

    private async Task<List<AccountEntryShare>> ValidateAndBuildSharesAsync(
        Guid userId, Guid workspaceId, SaveAccountEntryCommand command, CancellationToken cancellationToken)
    {
        if (command.PaymentMode == "full") return [];
        if (command.Type != EntryType.Expense)
        {
            throw AppException.BadRequest("只有支出可設定分攤。");
        }

        var workspace = await _workspaceRepository.GetActiveAsync(workspaceId, cancellationToken)
            ?? throw AppException.NotFound();
        if (workspace.Type != WorkspaceType.Family)
        {
            throw AppException.BadRequest("只有家庭帳本可設定分攤。");
        }
        if (command.Shares.Count == 0)
        {
            throw AppException.BadRequest("分攤至少需有一位參與者。");
        }
        if (command.Shares.Sum(x => x.Amount) != command.Amount)
        {
            throw AppException.BadRequest("所有分攤金額的合計必須等於總金額。");
        }

        var members = await _workspaceService.ListMembersAsync(userId, workspaceId, cancellationToken);
        var memberNames = members.ToDictionary(x => x.UserId, x => x.DisplayName);
        var seen = new HashSet<string>();
        var result = new List<AccountEntryShare>();
        foreach (var share in command.Shares)
        {
            if (share.Amount < 0) throw AppException.BadRequest("分攤金額不可小於 0。");
            if (share.UserId is Guid memberId)
            {
                if (!memberNames.TryGetValue(memberId, out var name)) throw AppException.BadRequest("分攤成員必須是此家庭的有效成員。");
                if (!seen.Add($"user:{memberId}")) throw AppException.BadRequest("同一位參與者不可重複設定。");
                result.Add(new AccountEntryShare { Id = Guid.NewGuid(), UserId = memberId, ParticipantName = name, Amount = share.Amount });
            }
            else
            {
                var name = share.ParticipantName?.Trim();
                if (string.IsNullOrWhiteSpace(name)) throw AppException.BadRequest("臨時參與者請填寫名稱。");
                if (!seen.Add($"guest:{name.ToUpperInvariant()}")) throw AppException.BadRequest("同一位參與者不可重複設定。");
                result.Add(new AccountEntryShare { Id = Guid.NewGuid(), ParticipantName = name, Amount = share.Amount });
            }
        }
        return result;
    }

    private static SettlementTransferDtoModel Transfer(string name, string currency, decimal amount) => new()
    {
        CounterpartyName = name,
        Currency = currency,
        Amount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero),
    };
}
