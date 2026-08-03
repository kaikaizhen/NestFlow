namespace NestFlow_Backend.Common;

/// <summary>
/// LINE Messaging API 設定。Channel Secret 與 Access Token 為機密值，
/// 只存在 appsettings.Development.json 或環境變數。
/// </summary>
public class LineMessagingOptions
{
    public const string SectionName = "LineMessaging";

    /// <summary>Messaging API Channel 的 Channel ID，作為身分綁定的 channel_id。</summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>用於驗證 Webhook 的 X-Line-Signature。</summary>
    public string ChannelSecret { get; set; } = string.Empty;

    /// <summary>呼叫 Reply 與 Push API 用的長期存取權杖。</summary>
    public string ChannelAccessToken { get; set; } = string.Empty;

    public string ReplyEndpoint { get; set; } = "https://api.line.me/v2/bot/message/reply";

    /// <summary>查詢官方帳號基本資料（含 basicId）的端點。</summary>
    public string BotInfoEndpoint { get; set; } = "https://api.line.me/v2/bot/info";

    /// <summary>
    /// LINE Login 與 Messaging API 是否屬於同一個 Provider。
    /// 同一 Provider 下使用者 ID 相同，可直接對應到既有帳號，不需另外綁定。
    /// </summary>
    public bool SharesProviderWithLogin { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ChannelSecret) && !string.IsNullOrWhiteSpace(ChannelAccessToken);
}
