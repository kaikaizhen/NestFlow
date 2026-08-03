namespace NestFlow_Backend.Common;

/// <summary>
/// Session Cookie 的統一寫入方式：HttpOnly、可設定 Secure、SameSite=Lax。
/// </summary>
public static class SessionCookieExtensions
{
    public static void AppendSessionCookie(
        this HttpResponse response,
        SessionOptions options,
        string sessionToken)
    {
        response.Cookies.Append(
            options.CookieName,
            sessionToken,
            BuildOptions(options, DateTimeOffset.UtcNow.AddDays(options.LifetimeDays)));
    }

    public static CookieOptions BuildOptions(SessionOptions options, DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        Secure = options.RequireHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expiresAt,
    };
}
