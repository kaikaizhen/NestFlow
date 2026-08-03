using NestFlow_Backend.Models.Dtos;

namespace NestFlow_Backend.Services;

public interface IAuthService
{
    /// <summary>建立 LINE 授權網址，同時回傳需暫存於瀏覽器的 state 與 nonce。</summary>
    (string AuthorizationUrl, string State, string Nonce) StartLineLogin();

    /// <summary>以授權碼完成登入，回傳 Session Token 明文（僅此一次）。</summary>
    Task<string> CompleteLineLoginAsync(string code, string nonce, CancellationToken cancellationToken);

    /// <summary>開發專用登入，不經過 LINE。僅在 Development 環境註冊。</summary>
    Task<string> DevLoginAsync(string externalSubject, string displayName, CancellationToken cancellationToken);

    /// <summary>依 Session Token 解析目前使用者，同時處理滑動對期。</summary>
    Task<Guid?> ResolveUserIdAsync(string sessionToken, CancellationToken cancellationToken);

    Task<CurrentUserDtoModel> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>更新使用者時區。資料仍以 UTC 保存，此設定只影響顯示與月份切分。</summary>
    Task UpdateTimeZoneAsync(Guid userId, string timeZone, CancellationToken cancellationToken);

    /// <summary>產生身分綁定碼，供在 LINE 對話中輸入以綁定帳號。明文只回傳一次。</summary>
    Task<BindingCodeDtoModel> CreateBindingCodeAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>取得 LINE 官方帳號資訊。未設定 Channel 或呼叫失敗時回傳 null。</summary>
    Task<LineBotDtoModel?> GetLineBotAsync(CancellationToken cancellationToken);

    Task LogoutAsync(string sessionToken, CancellationToken cancellationToken);
}
