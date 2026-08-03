using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Filters;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Models.ViewModels;
using NestFlow_Backend.Services;
using SessionOptions = NestFlow_Backend.Common.SessionOptions;

namespace NestFlow_Backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    /// <summary>暫存 state 與 nonce 的 Cookie，只在授權往返期間存在。</summary>
    private const string OAuthStateCookieName = "nestflow_oauth";

    private static readonly TimeSpan OAuthStateLifetime = TimeSpan.FromMinutes(10);

    private readonly IAuthService _authService;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ICryptoHelper _cryptoHelper;
    private readonly IMapper _mapper;
    private readonly ILogger<AuthController> _logger;
    private readonly LineLoginOptions _lineOptions;
    private readonly SessionOptions _sessionOptions;

    public AuthController(
        IAuthService authService,
        ICurrentUserAccessor currentUser,
        ICryptoHelper cryptoHelper,
        IMapper mapper,
        ILogger<AuthController> logger,
        IOptions<LineLoginOptions> lineOptions,
        IOptions<SessionOptions> sessionOptions)
    {
        _authService = authService;
        _currentUser = currentUser;
        _cryptoHelper = cryptoHelper;
        _mapper = mapper;
        _logger = logger;
        _lineOptions = lineOptions.Value;
        _sessionOptions = sessionOptions.Value;
    }

    /// <summary>導向 LINE 授權頁。</summary>
    [HttpGet("line/login")]
    public IActionResult LineLogin()
    {
        var (authorizationUrl, state, nonce) = _authService.StartLineLogin();

        // state 與 nonce 以加密 Cookie 暫存，回呼時比對，防止 CSRF 與重放
        Response.Cookies.Append(
            OAuthStateCookieName,
            _cryptoHelper.Encrypt($"{state}|{nonce}")!,
            BuildCookieOptions(DateTimeOffset.UtcNow.Add(OAuthStateLifetime)));

        return Redirect(authorizationUrl);
    }

    /// <summary>LINE 授權回呼。</summary>
    [HttpGet("line/callback")]
    public async Task<IActionResult> LineCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery(Name = "error")] string? error,
        CancellationToken cancellationToken)
    {
        var stateCookie = Request.Cookies[OAuthStateCookieName];
        Response.Cookies.Delete(OAuthStateCookieName);

        if (!string.IsNullOrEmpty(error))
        {
            throw AppException.Unauthorized("LINE 授權已取消或失敗。");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(stateCookie))
        {
            _logger.LogWarning(
                "LINE 回呼缺少必要參數。hasCode={HasCode} hasState={HasState} hasStateCookie={HasStateCookie}",
                !string.IsNullOrWhiteSpace(code),
                !string.IsNullOrWhiteSpace(state),
                !string.IsNullOrWhiteSpace(stateCookie));

            throw AppException.Unauthorized("登入驗證失敗，請重新登入。");
        }

        var (expectedState, nonce) = ReadStateCookie(stateCookie);

        if (!CryptographicEquals(expectedState, state))
        {
            _logger.LogWarning("LINE 回呼的 state 與暫存值不符，可能是 CSRF 或 Cookie 遺失。");
            throw AppException.Unauthorized("登入驗證失敗，請重新登入。");
        }

        var sessionToken = await _authService.CompleteLineLoginAsync(code, nonce, cancellationToken);
        AppendSessionCookie(sessionToken);

        return Redirect(_lineOptions.FrontendRedirectUrl);
    }

    /// <summary>取得目前登入使用者。</summary>
    [HttpGet("me")]
    [RequireSession]
    public async Task<ActionResult<CurrentUserViewModel>> Me(CancellationToken cancellationToken)
    {
        var dto = await _authService.GetCurrentUserAsync(_currentUser.RequireUserId(), cancellationToken);

        return Ok(_mapper.Map<CurrentUserViewModel>(dto));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[_sessionOptions.CookieName];

        if (!string.IsNullOrWhiteSpace(token))
        {
            await _authService.LogoutAsync(token, cancellationToken);
        }

        Response.Cookies.Delete(_sessionOptions.CookieName);

        return NoContent();
    }

    private (string State, string Nonce) ReadStateCookie(string cookieValue)
    {
        string? plain;

        try
        {
            plain = _cryptoHelper.Decrypt(cookieValue);
        }
        catch (Exception)
        {
            throw AppException.Unauthorized("登入驗證失敗，請重新登入。");
        }

        var parts = plain?.Split('|');

        if (parts is not { Length: 2 })
        {
            throw AppException.Unauthorized("登入驗證失敗，請重新登入。");
        }

        return (parts[0], parts[1]);
    }

    private void AppendSessionCookie(string sessionToken)
    {
        Response.AppendSessionCookie(_sessionOptions, sessionToken);
    }

    private CookieOptions BuildCookieOptions(DateTimeOffset expiresAt)
    {
        return SessionCookieExtensions.BuildOptions(_sessionOptions, expiresAt);
    }

    /// <summary>固定時間比對，避免以回應時間推測 state。</summary>
    private static bool CryptographicEquals(string left, string right)
    {
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(left),
            System.Text.Encoding.UTF8.GetBytes(right));
    }
}
