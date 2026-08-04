using NestFlow_Backend.Common;
using NestFlow_Backend.Services.External;

namespace NestFlow_Backend.Helpers;

/// <summary>
/// 把 Dify Workflow 回傳的未驗證結果轉成既有 FixedCommand。純函數，不存取資料庫。
/// 任何不符合規則的欄位都會回傳 null，交由呼叫端安全退回既有用法提示，
/// 日期時間範圍等既有規則沿用 LineWebhookService.TryResolvePeriod，這裡不重複驗證。
/// </summary>
public static class DifyResultMapper
{
    public static FixedCommand? ToFixedCommand(DifyCommandResult? result, decimal minConfidence)
    {
        if (result is null || result.Confidence < minConfidence)
        {
            return null;
        }

        return result.Kind switch
        {
            "entry" => ToEntryCommand(result),
            "event" => ToEventCommand(result),
            _ => null,
        };
    }

    private static FixedCommand? ToEntryCommand(DifyCommandResult result)
    {
        if (result.Amount is not > 0)
        {
            return null;
        }

        var type = result.Type switch
        {
            "expense" => EntryType.Expense,
            "income" => EntryType.Income,
            _ => (EntryType?)null,
        };

        if (type is null)
        {
            return null;
        }

        // 不信任 Dify 回傳的未知分類代碼，找不到就歸類為其他
        var known = AccountCategories.All.Any(c =>
            c.Type == type && string.Equals(c.Code, result.Category, StringComparison.OrdinalIgnoreCase));

        var (category, isFallback) = known
            ? (result.Category!, false)
            : (type == EntryType.Expense ? "other_expense" : "other_income", true);

        var note = string.IsNullOrWhiteSpace(result.Note) ? null : result.Note.Trim();

        return new FixedCommand(FixedCommandKind.Entry, type, result.Amount, category, note, isFallback);
    }

    private static FixedCommand? ToEventCommand(DifyCommandResult result)
    {
        var parsed = result.Event;

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Title))
        {
            return null;
        }

        var hasExplicitDate = parsed.Month is not null && parsed.Day is not null && parsed.DayOffset is null;
        var hasRelativeDate = parsed.Month is null && parsed.Day is null && parsed.DayOffset is not null;

        if (!hasExplicitDate && !hasRelativeDate)
        {
            return null;
        }

        if (parsed.StartHour is not { } startHour || parsed.StartMinute is not { } startMinute
            || startHour is < 0 or > 23 || startMinute is < 0 or > 59)
        {
            return null;
        }

        // 結束時間要嘛兩個欄位都給，要嘛都不給，避免半套資料誤導使用者
        var hasEnd = parsed.EndHour is not null || parsed.EndMinute is not null;

        if (hasEnd)
        {
            if (parsed.EndHour is not { } endHour || parsed.EndMinute is not { } endMinute
                || endHour is < 0 or > 23 || endMinute is < 0 or > 59)
            {
                return null;
            }
        }

        var parsedEvent = new ParsedEvent(
            parsed.Title.Trim(),
            parsed.DayOffset,
            parsed.Month,
            parsed.Day,
            startHour,
            startMinute,
            parsed.EndHour,
            parsed.EndMinute);

        return new FixedCommand(FixedCommandKind.Event, Event: parsedEvent);
    }
}
