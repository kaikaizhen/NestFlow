using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Services;
using SessionOptions = NestFlow_Backend.Common.SessionOptions;

namespace NestFlow_Backend.Middlewares;

/// <summary>
/// 讀取 Session Cookie 並解析出目前使用者，放入 HttpContext.Items。
/// 本中介軟體不負責拒絕請求，授權由 <see cref="Filters.RequireSessionAttribute"/> 判斷。
/// </summary>
public class SessionAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SessionOptions _options;

    public SessionAuthenticationMiddleware(RequestDelegate next, IOptions<SessionOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        var token = context.Request.Cookies[_options.CookieName];

        if (!string.IsNullOrWhiteSpace(token))
        {
            var userId = await authService.ResolveUserIdAsync(token, context.RequestAborted);

            if (userId is not null)
            {
                context.Items[CurrentUserAccessor.HttpContextItemKey] = userId.Value;
            }
            else
            {
                // Session 已失效，主動清除瀏覽器上的 Cookie
                context.Response.Cookies.Delete(_options.CookieName);
            }
        }

        await _next(context);
    }
}
