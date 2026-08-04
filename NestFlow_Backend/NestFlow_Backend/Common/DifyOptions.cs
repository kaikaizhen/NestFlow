namespace NestFlow_Backend.Common;

/// <summary>
/// Dify Workflow 設定。App Key 為機密值，
/// 只存在 appsettings.Development.json 或環境變數。
/// </summary>
public class DifyOptions
{
    public const string SectionName = "Dify";

    /// <summary>Dify API Base URL，例如 http://100.64.195.42/v1（不含結尾斜線）。</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>呼叫 /workflows/run 用的 App Key。</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>低於此信心值的解析結果一律視為無法判斷，安全退回既有用法提示。</summary>
    public decimal MinConfidence { get; set; } = 0.6m;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(AppKey);
}
