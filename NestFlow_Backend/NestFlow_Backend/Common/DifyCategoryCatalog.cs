using System.Text.Json;
using System.Text.Json.Serialization;

namespace NestFlow_Backend.Common;

/// <summary>
/// 把後端現有分類定義轉成 Dify Workflow inputs 需要的 JSON 字串，
/// 避免後端與 Dify 各自維護一份不同的分類規則。
/// </summary>
public static class DifyCategoryCatalog
{
    public static readonly string ExpenseCategoriesJson = BuildJson(EntryType.Expense);
    public static readonly string IncomeCategoriesJson = BuildJson(EntryType.Income);

    private static string BuildJson(EntryType type)
    {
        var payload = AccountCategories.All
            .Where(c => c.Type == type)
            .Select(c => new CategoryPayload(c.Code, c.Label, CategoryKeywords.KeywordsFor(c.Code)))
            .ToList();

        return JsonSerializer.Serialize(payload);
    }

    private sealed record CategoryPayload(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("keywords")] IReadOnlyList<string> Keywords);
}
