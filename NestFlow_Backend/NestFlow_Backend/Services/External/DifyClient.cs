using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;

namespace NestFlow_Backend.Services.External;

public class DifyClient : IDifyClient
{
    private readonly HttpClient _httpClient;
    private readonly DifyOptions _options;
    private readonly ILogger<DifyClient> _logger;

    public DifyClient(HttpClient httpClient, IOptions<DifyOptions> options, ILogger<DifyClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DifyCommandResult?> ParseAsync(DifyParseRequest request, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return null;
        }

        var payload = new DifyWorkflowRequest
        {
            Inputs = new Dictionary<string, object>
            {
                ["message"] = request.Message,
                ["current_date"] = request.CurrentDate,
                ["current_time"] = request.CurrentTime,
                ["time_zone"] = request.TimeZone,
                ["expense_categories"] = request.ExpenseCategoriesJson,
                ["income_categories"] = request.IncomeCategoriesJson,
            },
            User = request.UserId,
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.BaseUrl.TrimEnd('/')}/workflows/run")
            {
                Content = JsonContent.Create(payload),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AppKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Dify Workflow 呼叫失敗，狀態碼 {StatusCode}，回應內容：{Body}",
                    (int)response.StatusCode,
                    Truncate(body));
                return null;
            }

            var outer = await response.Content.ReadFromJsonAsync<DifyWorkflowResponse>(cancellationToken);
            var resultJson = outer?.Data?.Outputs is { } outputs && outputs.TryGetValue("result", out var element)
                ? element.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(resultJson))
            {
                _logger.LogWarning("Dify Workflow 回應缺少 outputs.result。");
                return null;
            }

            return JsonSerializer.Deserialize<DifyCommandResult>(resultJson);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // 呼叫失敗、逾時或格式無效都不應讓 Webhook 失敗，交由呼叫端退回既有用法提示
            _logger.LogWarning(ex, "無法呼叫 Dify Workflow 或解析回應。");
            return null;
        }
    }

    private static string Truncate(string text)
    {
        const int maxLength = 500;

        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private sealed class DifyWorkflowRequest
    {
        [JsonPropertyName("inputs")]
        public required Dictionary<string, object> Inputs { get; init; }

        [JsonPropertyName("response_mode")]
        public string ResponseMode { get; init; } = "blocking";

        [JsonPropertyName("user")]
        public required string User { get; init; }
    }

    private sealed class DifyWorkflowResponse
    {
        [JsonPropertyName("data")]
        public DifyWorkflowData? Data { get; init; }
    }

    private sealed class DifyWorkflowData
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("outputs")]
        public Dictionary<string, JsonElement>? Outputs { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
