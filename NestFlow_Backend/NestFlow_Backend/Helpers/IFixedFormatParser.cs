using NestFlow_Backend.Common;

namespace NestFlow_Backend.Helpers;

/// <summary>固定格式訊息的解析結果種類。</summary>
public enum FixedCommandKind
{
    /// <summary>不符合任何固定格式。第一版直接回覆用法，Module 9 起才改呼叫 Dify。</summary>
    None,
    Entry,
    Confirm,
    Cancel,
    Help,
    Binding,
}

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
    string? BindingCode = null);

/// <summary>
/// 固定格式訊息解析器。純函數，無商業邏輯、不存取資料庫。
/// </summary>
public interface IFixedFormatParser
{
    FixedCommand Parse(string message);
}
