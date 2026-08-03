using System.Globalization;
using System.Text.RegularExpressions;
using NestFlow_Backend.Common;

namespace NestFlow_Backend.Helpers;

/// <summary>
/// 解析計畫第 11 節定義的固定格式：
///   記帳 午餐 120
///   支出 交通 60 捷運
///   收入 薪資 50000
///   確認
///   取消
/// 「記帳」視同支出。金額可帶小數與千分位逗號。
/// </summary>
public partial class FixedFormatParser : IFixedFormatParser
{
    private static readonly string[] ExpenseKeywords = ["記帳", "支出", "花費"];
    private static readonly string[] IncomeKeywords = ["收入", "進帳"];
    private static readonly string[] ConfirmKeywords = ["確認", "確定", "yes", "y", "ok"];
    private static readonly string[] CancelKeywords = ["取消", "no", "n"];
    private static readonly string[] HelpKeywords = ["說明", "help", "?", "？", "指令"];

    public FixedCommand Parse(string message)
    {
        var normalized = Normalize(message);

        if (normalized.Length == 0)
        {
            return new FixedCommand(FixedCommandKind.None);
        }

        if (ConfirmKeywords.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return new FixedCommand(FixedCommandKind.Confirm);
        }

        if (CancelKeywords.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return new FixedCommand(FixedCommandKind.Cancel);
        }

        if (HelpKeywords.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return new FixedCommand(FixedCommandKind.Help);
        }

        // 綁定碼為 6 碼大寫英數，或「綁定 ABC123」
        var bindingMatch = BindingRegex().Match(normalized);
        if (bindingMatch.Success)
        {
            return new FixedCommand(
                FixedCommandKind.Binding,
                BindingCode: bindingMatch.Groups["code"].Value.ToUpperInvariant());
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length < 2)
        {
            return new FixedCommand(FixedCommandKind.None);
        }

        EntryType type;

        if (ExpenseKeywords.Contains(tokens[0], StringComparer.OrdinalIgnoreCase))
        {
            type = EntryType.Expense;
        }
        else if (IncomeKeywords.Contains(tokens[0], StringComparer.OrdinalIgnoreCase))
        {
            type = EntryType.Income;
        }
        else
        {
            return new FixedCommand(FixedCommandKind.None);
        }

        // 找出第一個可解析為金額的欄位，前面的視為分類、後面的視為備註
        var amountIndex = -1;
        decimal amount = 0;

        for (var i = 1; i < tokens.Length; i++)
        {
            if (TryParseAmount(tokens[i], out amount))
            {
                amountIndex = i;
                break;
            }
        }

        if (amountIndex < 0 || amount <= 0)
        {
            return new FixedCommand(FixedCommandKind.None);
        }

        var keyword = amountIndex > 1 ? string.Join(' ', tokens[1..amountIndex]) : null;
        var noteTokens = tokens[(amountIndex + 1)..];

        var (category, isFallback) = CategoryKeywords.Resolve(keyword, type);

        // 未命中對應表時，把原詞保留在備註開頭，避免使用者輸入的資訊遺失
        var noteParts = new List<string>();
        if (isFallback && !string.IsNullOrWhiteSpace(keyword))
        {
            noteParts.Add(keyword);
        }

        noteParts.AddRange(noteTokens);

        var note = noteParts.Count > 0 ? string.Join(' ', noteParts) : null;

        return new FixedCommand(
            FixedCommandKind.Entry,
            type,
            decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            category,
            note,
            isFallback);
    }

    /// <summary>統一全形空白與全形數字，並壓縮連續空白。</summary>
    private static string Normalize(string message)
    {
        var trimmed = (message ?? string.Empty).Replace('　', ' ').Trim();

        var converted = string.Create(trimmed.Length, trimmed, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                // 全形數字、逗號與句點轉為半形
                span[i] = c switch
                {
                    >= '０' and <= '９' => (char)(c - '０' + '0'),
                    '，' => ',',
                    '．' => '.',
                    _ => c,
                };
            }
        });

        return WhitespaceRegex().Replace(converted, " ");
    }

    private static bool TryParseAmount(string token, out decimal amount)
    {
        // 允許千分位逗號與貨幣符號
        var cleaned = token.Replace(",", string.Empty).TrimStart('$', '＄');

        return decimal.TryParse(
            cleaned,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out amount);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?:綁定\s*)?(?<code>[0-9A-Za-z]{6})$")]
    private static partial Regex BindingRegex();
}
