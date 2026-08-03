using NestFlow_Backend.Common;

namespace NestFlow_Backend.Helpers;

/// <summary>固定格式訊息的解析結果種類。</summary>
public enum FixedCommandKind
{
    /// <summary>不符合任何固定格式。第一版直接回覆用法，Module 9 起才改呼叫 Dify。</summary>
    None,
    Entry,
    Event,
    Confirm,
    Cancel,
    Help,
    Binding,
}

/// <summary>
/// 行程訊息解析出的原始欄位。解析器不知道使用者時區，
/// 因此只回傳字面資訊，實際換算為 UTC 由 Service 依使用者時區完成。
/// <para><c>DayOffset</c> 為相對天數：今天 0、明天 1、後天 2；有指定 <c>Month</c> 與 <c>Day</c> 時為 null。</para>
/// </summary>
public record ParsedEvent(
    string Title,
    int? DayOffset,
    int? Month,
    int? Day,
    int StartHour,
    int StartMinute,
    int? EndHour,
    int? EndMinute);

/// <summary>
/// 固定格式解析結果。純資料，不含任何寫入行為。
/// </summary>
public record FixedCommand(
    FixedCommandKind Kind,
    EntryType? Type = null,
    decimal? Amount = null,
    string? Category = null,
    string? Note = null,
    bool CategoryIsFallback = false,
    string? BindingCode = null,
    ParsedEvent? Event = null);

/// <summary>
/// 固定格式訊息解析器。純函數，無商業邏輯、不存取資料庫。
/// </summary>
public interface IFixedFormatParser
{
    FixedCommand Parse(string message);
}
