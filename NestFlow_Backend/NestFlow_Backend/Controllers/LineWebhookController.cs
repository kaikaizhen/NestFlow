using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Services;
using NestFlow_Backend.Services.External;

namespace NestFlow_Backend.Controllers;

/// <summary>
/// LINE Messaging API Webhook。
/// 一律先驗證簽章，且無論處理結果為何都回 200，避免 LINE 反覆重送。
/// </summary>
[ApiController]
[Route("api/webhooks/line")]
public class LineWebhookController : ControllerBase
{
    /// <summary>LINE 送出的是 camelCase JSON，需忽略大小寫才能對應到屬性。</summary>
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILineWebhookService _webhookService;
    private readonly ILineMessagingClient _messagingClient;
    private readonly ILineSignatureValidator _signatureValidator;
    private readonly LineMessagingOptions _options;
    private readonly ILogger<LineWebhookController> _logger;

    public LineWebhookController(
        ILineWebhookService webhookService,
        ILineMessagingClient messagingClient,
        ILineSignatureValidator signatureValidator,
        IOptions<LineMessagingOptions> options,
        ILogger<LineWebhookController> logger)
    {
        _webhookService = webhookService;
        _messagingClient = messagingClient;
        _signatureValidator = signatureValidator;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var body = buffer.ToArray();

        var signature = Request.Headers["X-Line-Signature"].FirstOrDefault();

        if (!_signatureValidator.IsValid(_options.ChannelSecret, body, signature))
        {
            _logger.LogWarning("LINE Webhook 簽章驗證失敗，已拒絕。");
            return Unauthorized();
        }

        await ProcessAsync(body, cancellationToken);

        return Ok();
    }

    private async Task ProcessAsync(byte[] body, CancellationToken cancellationToken)
    {
        LineWebhookPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<LineWebhookPayload>(body, PayloadOptions);
        }
        catch (JsonException)
        {
            _logger.LogWarning("LINE Webhook 內容不是合法 JSON。");
            return;
        }

        foreach (var lineEvent in payload?.Events ?? [])
        {
            // 第一版只處理文字訊息，其他事件忽略但仍回 200
            if (lineEvent.Type != "message"
                || lineEvent.Message?.Type != "text"
                || string.IsNullOrWhiteSpace(lineEvent.Source?.UserId)
                || string.IsNullOrWhiteSpace(lineEvent.WebhookEventId))
            {
                continue;
            }

            try
            {
                var reply = await _webhookService.HandleMessageAsync(
                    new IncomingMessage(
                        lineEvent.WebhookEventId,
                        lineEvent.Source.UserId,
                        lineEvent.Message.Text ?? string.Empty,
                        lineEvent.ReplyToken),
                    cancellationToken);

                if (reply is not null && !string.IsNullOrWhiteSpace(lineEvent.ReplyToken))
                {
                    await _messagingClient.ReplyAsync(lineEvent.ReplyToken, reply, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // 單一事件失敗不影響其他事件，也不讓 LINE 重送
                _logger.LogError(ex, "處理 LINE 事件時發生錯誤。");
            }
        }
    }

    public class LineWebhookPayload
    {
        public List<LineEvent>? Events { get; set; }
    }

    public class LineEvent
    {
        public string? Type { get; set; }

        public string? WebhookEventId { get; set; }

        public string? ReplyToken { get; set; }

        public LineSource? Source { get; set; }

        public LineMessage? Message { get; set; }
    }

    public class LineSource
    {
        public string? Type { get; set; }

        public string? UserId { get; set; }
    }

    public class LineMessage
    {
        public string? Type { get; set; }

        public string? Text { get; set; }
    }
}
