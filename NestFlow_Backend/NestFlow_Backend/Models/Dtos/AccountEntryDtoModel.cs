using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Dtos;

public class AccountEntryDtoModel
{
    public Guid Id { get; set; }

    public EntryType Type { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;
}

/// <summary>單一幣別的月度統計。</summary>
public class CurrencySummaryDtoModel
{
    public string Currency { get; set; } = string.Empty;

    public decimal Income { get; set; }

    public decimal Expense { get; set; }

    public decimal Balance { get; set; }
}

/// <summary>記帳寫入用的命令，欄位皆已通過驗證與正規化。</summary>
public record SaveAccountEntryCommand(
    EntryType Type,
    decimal Amount,
    string Currency,
    string Category,
    string? Note,
    DateTimeOffset OccurredAtUtc);
