using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NestFlow_Backend.Common;

namespace NestFlow_Backend.Services.External;

public class LineLoginClient : ILineLoginClient
{
    private readonly HttpClient _httpClient;
    private readonly LineLoginOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly ILogger<LineLoginClient>? _logger;

    public LineLoginClient(
        HttpClient httpClient,
        IOptions<LineLoginOptions> options,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
        ILogger<LineLoginClient>? logger = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public string BuildAuthorizationUrl(string state, string nonce)
    {
        EnsureConfigured();

        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ChannelId,
            ["redirect_uri"] = _options.CallbackUrl,
            ["state"] = state,
            ["scope"] = "openid profile",
            ["nonce"] = nonce,
        };

        var queryString = string.Join(
            '&',
            query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));

        return $"{_options.AuthorizationEndpoint}?{queryString}";
    }

    public async Task<LineUserProfile> ExchangeCodeAsync(
        string code,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _options.CallbackUrl,
                ["client_id"] = _options.ChannelId,
                ["client_secret"] = _options.ChannelSecret,
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // 不記錄回應內容，避免 Channel Secret 或授權碼進入日誌
            throw AppException.Unauthorized("LINE 授權失敗，請重新登入。");
        }

        var payload = await response.Content.ReadFromJsonAsync<LineTokenResponse>(cancellationToken)
            ?? throw AppException.Unauthorized("LINE 授權失敗，請重新登入。");

        if (string.IsNullOrWhiteSpace(payload.IdToken))
        {
            throw AppException.Unauthorized("LINE 未回傳 ID Token。");
        }

        return await ValidateIdTokenAsync(payload.IdToken, expectedNonce, cancellationToken);
    }

    private async Task<LineUserProfile> ValidateIdTokenAsync(
        string idToken,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);

        // LINE 依 Channel 設定可能以 ES256（JWKS 公鑰）或 HS256（Channel Secret）簽章，
        // 兩種金鑰都提供，由 Token header 的 alg 決定實際使用哪一把。
        var signingKeys = new List<SecurityKey>(configuration.SigningKeys)
        {
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ChannelSecret)),
        };

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.ChannelId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            // 明確限制演算法，避免 alg 混淆攻擊
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        var handler = new JwtSecurityTokenHandler();

        try
        {
            handler.ValidateToken(idToken, parameters, out var validatedToken);

            var jwt = (JwtSecurityToken)validatedToken;

            var nonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
            if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
            {
                _logger?.LogWarning("ID Token 的 nonce 與預期不符，拒絕登入。");
                throw AppException.Unauthorized("登入驗證失敗，請重新登入。");
            }

            var subject = jwt.Subject;
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw AppException.Unauthorized("登入驗證失敗，請重新登入。");
            }

            return new LineUserProfile(
                subject,
                jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "LINE 使用者",
                jwt.Claims.FirstOrDefault(c => c.Type == "picture")?.Value);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            // 格式錯誤、簽章不符、逾期等一律視為登入失敗。
            // 原因只寫入伺服端日誌，回給使用者的訊息不含細節。
            _logger?.LogWarning(ex, "ID Token 驗證失敗。");
            throw AppException.Unauthorized("登入驗證失敗，請重新登入。");
        }
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw AppException.BadRequest(
                "LINE Login 尚未設定，請於 appsettings.Development.json 填入 LineLogin 的 ChannelId、ChannelSecret 與 CallbackUrl。");
        }
    }

    private class LineTokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }
}
