using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Services;
using SessionOptions = NestFlow_Backend.Common.SessionOptions;

namespace NestFlow_Backend.Controllers;

/// <summary>
/// 開發專用登入，讓尚未取得 LINE Channel 時仍可測試 Workspace 與家庭機制。
/// 僅在 Development 環境註冊路由，其他環境不存在此端點。
/// </summary>
[ApiController]
[Route("api/dev/auth")]
public class DevAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;
    private readonly SessionOptions _sessionOptions;

    public DevAuthController(
        IAuthService authService,
        IWebHostEnvironment environment,
        IOptions<SessionOptions> sessionOptions)
    {
        _authService = authService;
        _environment = environment;
        _sessionOptions = sessionOptions.Value;
    }

    [HttpPost("login")]
    public async Task<ActionResult<DevLoginResultViewModel>> Login(
        [FromBody] DevLoginParamModel param,
        CancellationToken cancellationToken)
    {
        // 非開發環境時此端點視同不存在
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        var sessionToken = await _authService.DevLoginAsync(
            param.ExternalSubject,
            param.DisplayName,
            cancellationToken);

        Response.AppendSessionCookie(_sessionOptions, sessionToken);

        return Ok(new DevLoginResultViewModel { DisplayName = param.DisplayName });
    }

    public class DevLoginParamModel
    {
        /// <summary>模擬的 LINE 使用者識別碼，相同值代表同一位使用者。</summary>
        [Required(ErrorMessage = "請輸入識別碼。")]
        [StringLength(64, MinimumLength = 1)]
        public string ExternalSubject { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入顯示名稱。")]
        [StringLength(100, MinimumLength = 1)]
        public string DisplayName { get; set; } = string.Empty;
    }

    public class DevLoginResultViewModel
    {
        public string DisplayName { get; set; } = string.Empty;
    }
}
