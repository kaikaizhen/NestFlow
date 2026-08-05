namespace NestFlow_Backend.Services.External;

/// <summary>
/// 自然語言解析提供者。第一版唯一實作為 Module 8 的 DifyAssistantProvider。
/// 提供者只負責解析語意，不得回傳 UserId／WorkspaceId，也不得存取資料庫。
/// </summary>
public interface IAssistantProvider
{
    Task<AssistantResult> ParseAsync(
        AssistantRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// 送往解析提供者的輸入。欄位對應計畫第 9 節 Workflow 輸入。
/// </summary>
public record AssistantRequest(
    string Message,
    string Locale,
    string TimeZone,
    DateTimeOffset CurrentTime,
    IReadOnlyCollection<string> AllowedIntents,
    string SchemaVersion);

/// <summary>
/// 解析提供者的固定輸出。Data 於 Module 8 才依 Intent 收斂為強型別命令。
/// </summary>
public record AssistantResult(
    string SchemaVersion,
    string WorkflowVersion,
    string Intent,
    decimal Confidence,
    bool RequiresConfirmation,
    IReadOnlyDictionary<string, object?> Data,
    IReadOnlyCollection<string> MissingFields,
    string Reply);
