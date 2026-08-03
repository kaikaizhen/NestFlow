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

    Task LogoutAsync(string sessionToken, CancellationToken cancellationToken);
}
