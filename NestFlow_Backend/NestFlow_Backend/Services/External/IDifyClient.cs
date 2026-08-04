using System.Text.Json.Serialization;

namespace NestFlow_Backend.Services.External;

/// <summary>呼叫 Dify Workflow 解析自然語言訊息所需的輸入。</summary>
public record DifyParseRequest(
    string Message,
    string CurrentDate,
    string CurrentTime,
    string TimeZone,
    string ExpenseCategoriesJson,
    string IncomeCategoriesJson,
    string UserId);

/// <summary>
/// Dify Workflow 對外呼叫。抽為介面以便測試時替換，
/// 不受信任的輸出一律由呼叫端驗證後才轉成既有 FixedCommand。
/// </summary>
public interface IDifyClient
{
    /// <summary>
    /// 呼叫 Dify Workflow 解析訊息。尚未設定、呼叫失敗、逾時或回應格式無效時回傳 null，
    /// 由呼叫端安全退回既有用法提示，不會讓 Webhook 因此失敗。
    /// </summary>
    Task<DifyCommandResult?> ParseAsync(DifyParseRequest request, CancellationToken cancellationToken);
}

/// <summary>Dify outputs.result 反序列化後的解析結果。純資料，未經驗證，不可直接信任。</summary>
public sealed class DifyCommandResult
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }

    [JsonPropertyName("categoryIsFallback")]
    public bool CategoryIsFallback { get; init; }

    [JsonPropertyName("event")]
    public DifyEventResult? Event { get; init; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

public sealed class DifyEventResult
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("month")]
    public int? Month { get; init; }

    [JsonPropertyName("day")]
    public int? Day { get; init; }

    [JsonPropertyName("dayOffset")]
    public int? DayOffset { get; init; }

    [JsonPropertyName("startHour")]
    public int? StartHour { get; init; }

    [JsonPropertyName("startMinute")]
    public int? StartMinute { get; init; }

    [JsonPropertyName("endHour")]
    public int? EndHour { get; init; }

    [JsonPropertyName("endMinute")]
    public int? EndMinute { get; init; }
}
