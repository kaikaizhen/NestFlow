namespace NestFlow_Backend.Common;

/// <summary>
/// 記帳分類固定清單。分類以代碼保存，顯示名稱與圖示由前端依代碼對應。
/// Module 4 的 LINE 固定格式解析也會對應到同一份清單。
/// </summary>
public static class AccountCategories
{
    public record CategoryDefinition(string Code, string Label, EntryType Type);

    public static readonly IReadOnlyList<CategoryDefinition> All =
    [
        new("food", "餐飲", EntryType.Expense),
        new("transport", "交通", EntryType.Expense),
        new("shopping", "購物", EntryType.Expense),
        new("home", "居家", EntryType.Expense),
        new("medical", "醫療", EntryType.Expense),
        new("entertainment", "娛樂", EntryType.Expense),
        new("other_expense", "其他支出", EntryType.Expense),

        new("salary", "薪資", EntryType.Income),
        new("bonus", "獎金", EntryType.Income),
        new("investment", "投資", EntryType.Income),
        new("other_income", "其他收入", EntryType.Income),
    ];

    private static readonly Dictionary<string, CategoryDefinition> ByCode =
        All.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>驗證分類代碼存在且與記帳類型相符，回傳正規化後的代碼。</summary>
    public static string Normalize(string? code, EntryType type)
    {
        if (string.IsNullOrWhiteSpace(code) || !ByCode.TryGetValue(code.Trim(), out var definition))
        {
            throw AppException.BadRequest("分類不存在。");
        }

        if (definition.Type != type)
        {
            throw AppException.BadRequest(
                type == EntryType.Expense ? "此分類不能用於支出。" : "此分類不能用於收入。");
        }

        return definition.Code;
    }
}
