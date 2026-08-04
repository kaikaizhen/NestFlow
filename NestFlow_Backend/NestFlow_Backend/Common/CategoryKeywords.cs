namespace NestFlow_Backend.Common;

/// <summary>
/// 自然語彙對應到固定分類代碼。供 LINE 固定格式解析使用。
/// 未命中時歸類為「其他」，原詞保留於備註，不會讓使用者的輸入遺失。
/// </summary>
public static class CategoryKeywords
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // 餐飲
        ["餐飲"] = "food",
        ["吃飯"] = "food",
        ["早餐"] = "food",
        ["午餐"] = "food",
        ["晚餐"] = "food",
        ["宵夜"] = "food",
        ["飲料"] = "food",
        ["咖啡"] = "food",
        ["點心"] = "food",
        ["外送"] = "food",

        // 交通
        ["交通"] = "transport",
        ["捷運"] = "transport",
        ["公車"] = "transport",
        ["火車"] = "transport",
        ["高鐵"] = "transport",
        ["計程車"] = "transport",
        ["加油"] = "transport",
        ["停車"] = "transport",
        ["油錢"] = "transport",

        // 購物
        ["購物"] = "shopping",
        ["超市"] = "shopping",
        ["網購"] = "shopping",
        ["衣服"] = "shopping",
        ["日用品"] = "shopping",
        ["賣場"] = "shopping",

        // 居家
        ["居家"] = "home",
        ["房租"] = "home",
        ["水費"] = "home",
        ["電費"] = "home",
        ["瓦斯"] = "home",
        ["網路費"] = "home",
        ["家具"] = "home",

        // 醫療
        ["醫療"] = "medical",
        ["看病"] = "medical",
        ["買藥"] = "medical",
        ["藥局"] = "medical",
        ["健檢"] = "medical",
        ["牙醫"] = "medical",

        // 娛樂
        ["娛樂"] = "entertainment",
        ["電影"] = "entertainment",
        ["遊戲"] = "entertainment",
        ["旅遊"] = "entertainment",
        ["訂閱"] = "entertainment",
        ["運動"] = "entertainment",

        // 收入
        ["薪資"] = "salary",
        ["薪水"] = "salary",
        ["月薪"] = "salary",
        ["獎金"] = "bonus",
        ["紅利"] = "bonus",
        ["年終"] = "bonus",
        ["投資"] = "investment",
        ["股利"] = "investment",
        ["股息"] = "investment",
        ["利息"] = "investment",
    };

    /// <summary>取得對應到某分類代碼的所有關鍵字，供 Dify Workflow 分類目錄使用。</summary>
    public static IReadOnlyList<string> KeywordsFor(string code)
    {
        return Map.Where(kv => kv.Value == code).Select(kv => kv.Key).ToList();
    }

    /// <summary>
    /// 解析關鍵字。回傳分類代碼，以及是否為推測（未命中對應表）。
    /// </summary>
    public static (string Category, bool IsFallback) Resolve(string? keyword, EntryType type)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();

            // 先試分類代碼本身，再試中文關鍵字
            var byCode = AccountCategories.All.FirstOrDefault(
                x => string.Equals(x.Code, trimmed, StringComparison.OrdinalIgnoreCase) && x.Type == type);

            if (byCode is not null)
            {
                return (byCode.Code, false);
            }

            if (Map.TryGetValue(trimmed, out var code))
            {
                var definition = AccountCategories.All.First(x => x.Code == code);

                if (definition.Type == type)
                {
                    return (code, false);
                }
            }
        }

        return (type == EntryType.Expense ? "other_expense" : "other_income", true);
    }
}
