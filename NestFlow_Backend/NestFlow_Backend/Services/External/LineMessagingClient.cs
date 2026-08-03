using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;

namespace NestFlow_Backend.Services.External;

public class LineMessagingClient : ILineMessagingClient
{
    /// <summary>LINE 單則文字訊息上限為 5000 字元。</summary>
    private const int MaxTextLength = 5000;

    private readonly HttpClient _httpClient;
    private readonly LineMessagingOptions _options;
    private readonly ILogger<LineMessagingClient> _logger;

    public LineMessagingClient(
        HttpClient httpClient,
        IOptions<LineMessagingOptions> options,
        ILogger<LineMessagingClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ReplyAsync(string replyToken, string text, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            // 尚未設定 Messaging Channel 時不阻斷流程，僅記錄，方便以模擬端點開發
            _logger.LogInformation("LINE Messaging 尚未設定，略過回覆。");
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            replyToken,
            messages = new[]
            {
                new { type = "text", text = Truncate(text) },
            },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ReplyEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // 回覆失敗不應讓 Webhook 回傳錯誤，否則 LINE 會不斷重送
            _logger.LogWarning("LINE 回覆失敗，狀態碼 {StatusCode}。", (int)response.StatusCode);
        }
    }

    public async Task<LineBotInfo?> GetBotInfoAsync(CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _options.BotInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("取得 LINE 官方帳號資訊失敗，狀態碼 {StatusCode}。", (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<BotInfoResponse>(cancellationToken);

            return string.IsNullOrWhiteSpace(payload?.BasicId)
                ? null
                : new LineBotInfo(payload.BasicId, payload.DisplayName ?? string.Empty, payload.PictureUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // 取不到只影響加好友入口的顯示，不應讓設定頁整頁失敗
            _logger.LogWarning(ex, "無法連線 LINE 取得官方帳號資訊。");
            return null;
        }
    }

    private static string Truncate(string text)
    {
        return text.Length <= MaxTextLength ? text : text[..MaxTextLength];
    }

    /// <summary>/v2/bot/info 的回應。只取顯示加好友入口需要的欄位。</summary>
    private class BotInfoResponse
    {
        [JsonPropertyName("basicId")]
        public string? BasicId { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("pictureUrl")]
        public string? PictureUrl { get; set; }
    }
}
