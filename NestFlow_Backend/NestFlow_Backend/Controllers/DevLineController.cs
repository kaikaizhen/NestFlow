using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NestFlow_Backend.Services;

namespace NestFlow_Backend.Controllers;

/// <summary>
/// 開發專用的 LINE 訊息模擬端點。
/// 讓尚未取得 Messaging API Channel 時仍可測試完整的解析、待確認與寫入流程。
/// 僅在 Development 與 Testing 環境生效。
/// </summary>
[ApiController]
[Route("api/dev/line")]
public class DevLineController : ControllerBase
{
    private readonly ILineWebhookService _webhookService;
    private readonly IWebHostEnvironment _environment;

    public DevLineController(ILineWebhookService webhookService, IWebHostEnvironment environment)
    {
        _webhookService = webhookService;
        _environment = environment;
    }

    [HttpPost("message")]
    public async Task<ActionResult<SimulateResultViewModel>> Message(
        [FromBody] SimulateParamModel param,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        var reply = await _webhookService.HandleMessageAsync(
            new IncomingMessage(
                // 未指定事件代碼時自動產生，模擬每次都是新事件
                string.IsNullOrWhiteSpace(param.EventId) ? Guid.NewGuid().ToString("N") : param.EventId,
                param.ExternalUserId,
                param.Text,
                ReplyToken: null),
            cancellationToken);

        return Ok(new SimulateResultViewModel { Reply = reply });
    }

    public class SimulateParamModel
    {
        /// <summary>模擬的 LINE 使用者 ID。與開發登入使用相同值即代表同一人。</summary>
        [Required]
        [StringLength(64, MinimumLength = 1)]
        public string ExternalUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Text { get; set; } = string.Empty;

        /// <summary>指定相同的事件代碼可測試冪等行為。</summary>
        public string? EventId { get; set; }
    }

    public class SimulateResultViewModel
    {
        /// <summary>要回覆給使用者的文字。null 代表事件被忽略。</summary>
        public string? Reply { get; set; }
    }
}
