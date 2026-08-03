namespace NestFlow_Backend.Common;

/// <summary>
/// 編譯期常數，不來自設定檔。
/// </summary>
public static class GlobalConstants
{
    /// <summary>Health Check 端點：僅檢查行程本身是否存活。</summary>
    public const string LivenessEndpoint = "/health/live";

    /// <summary>Health Check 端點：檢查外部相依（資料庫）是否就緒。</summary>
    public const string ReadinessEndpoint = "/health/ready";

    /// <summary>只檢查行程本身的 Health Check 標籤。</summary>
    public const string LiveTag = "live";

    /// <summary>檢查外部相依的 Health Check 標籤。</summary>
    public const string ReadyTag = "ready";
}
