namespace NestFlow_Backend.Models.ViewModels;

public class AccountEntryViewModel
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;

    public string PaymentMode { get; set; } = "full";

    public bool IsSettled { get; set; }

    public List<AccountEntryShareViewModel> Shares { get; set; } = [];
}

public class AccountEntryShareViewModel
{
    public Guid? UserId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class CurrencySummaryViewModel
{
    public string Currency { get; set; } = string.Empty;

    public decimal Income { get; set; }

    public decimal Expense { get; set; }

    public decimal Balance { get; set; }
}

public class CategoryViewModel
{
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;
}
