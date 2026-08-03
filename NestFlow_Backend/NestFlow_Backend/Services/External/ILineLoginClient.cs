namespace NestFlow_Backend.Services.External;

/// <summary>
/// LINE Login OAuth／OIDC 對外呼叫。抽為介面以便測試時替換。
/// </summary>
public interface ILineLoginClient
{
    /// <summary>組出 LINE 授權頁網址，內含 state 與 nonce。</summary>
    string BuildAuthorizationUrl(string state, string nonce);

    /// <summary>以授權碼換取 Token，並驗證 ID Token 後回傳使用者資料。</summary>
    Task<LineUserProfile> ExchangeCodeAsync(string code, string expectedNonce, CancellationToken cancellationToken);
}

/// <summary>ID Token 驗證通過後取得的 LINE 使用者資料。</summary>
public record LineUserProfile(string Subject, string DisplayName, string? PictureUrl);
