namespace NestFlow_Backend.Common;

/// <summary>
/// 支援的幣別。第一版不做匯率換算，統計時各幣別分開加總。
/// </summary>
public static class SupportedCurrencies
{
    public const string Default = "TWD";

    public static readonly IReadOnlyList<string> All = ["TWD", "USD", "JPY", "EUR", "CNY"];

    private static readonly HashSet<string> Lookup = new(All, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return Default;
        }

        var normalized = currency.Trim().ToUpperInvariant();

        if (!Lookup.Contains(normalized))
        {
            throw AppException.BadRequest("不支援的幣別。");
        }

        return normalized;
    }
}
