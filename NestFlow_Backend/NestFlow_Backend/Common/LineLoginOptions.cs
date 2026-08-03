namespace NestFlow_Backend.Common;

/// <summary>
/// LINE Login 設定。Channel Secret 為機密值，只存在 appsettings.Development.json 或環境變數。
/// </summary>
public class LineLoginOptions
{
    public const string SectionName = "LineLogin";

    public string ChannelId { get; set; } = string.Empty;

    public string ChannelSecret { get; set; } = string.Empty;

    /// <summary>LINE 後台登記的 Callback URL，必須與送出的 redirect_uri 完全一致。</summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>登入成功後導回的前端網址。</summary>
    public string FrontendRedirectUrl { get; set; } = "http://localhost:5173/";

    public string AuthorizationEndpoint { get; set; } = "https://access.line.me/oauth2/v2.1/authorize";

    public string TokenEndpoint { get; set; } = "https://api.line.me/oauth2/v2.1/token";

    /// <summary>
    /// OpenID Connect discovery 文件位址。簽章金鑰由此文件的 jwks_uri 取得並自動快取與輪替。
    /// 注意：這裡必須是 discovery 文件，不能直接填 JWKS 位址。
    /// </summary>
    public string MetadataAddress { get; set; } = "https://access.line.me/.well-known/openid-configuration";

    public string Issuer { get; set; } = "https://access.line.me";

    /// <summary>是否已提供可用的 Channel 設定。</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ChannelId)
        && !string.IsNullOrWhiteSpace(ChannelSecret)
        && !string.IsNullOrWhiteSpace(CallbackUrl);
}
