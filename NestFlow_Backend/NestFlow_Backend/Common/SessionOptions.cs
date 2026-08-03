namespace NestFlow_Backend.Common;

/// <summary>
/// Server-side Session 設定。Session Token 只以雜湊保存於資料庫。
/// </summary>
public class SessionOptions
{
    public const string SectionName = "Session";

    public string CookieName { get; set; } = "nestflow_session";

    /// <summary>Session 有效天數，每次存取自動延長（滑動對期）。</summary>
    public int LifetimeDays { get; set; } = 30;

    /// <summary>
    /// 是否要求 Cookie 只在 HTTPS 傳送。本機以 HTTP 測試時可關閉，正式環境必須為 true。
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>剩餘效期低於此比例時才更新資料庫，避免每次請求都寫入。</summary>
    public double SlidingRenewThreshold { get; set; } = 0.5;
}
