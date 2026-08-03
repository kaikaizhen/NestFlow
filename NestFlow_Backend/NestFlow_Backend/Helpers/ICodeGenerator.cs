namespace NestFlow_Backend.Helpers;

/// <summary>
/// 以密碼學亂數產生一次性代碼。無商業邏輯、不存取資料庫。
/// </summary>
public interface ICodeGenerator
{
    /// <summary>產生 8 碼大寫英數邀請碼，已排除 0、O、1、I、L 等易混字元。</summary>
    string GenerateInvitationCode();

    /// <summary>產生 Session Token（URL-safe Base64，256 bits）。</summary>
    string GenerateSessionToken();

    /// <summary>產生 OAuth 用的隨機字串，供 state 與 nonce 使用。</summary>
    string GenerateOAuthValue();
}
