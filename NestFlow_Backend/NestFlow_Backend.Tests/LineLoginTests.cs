using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NestFlow_Backend.Common;
using NestFlow_Backend.Services.External;

namespace NestFlow_Backend.Tests;

public class LineLoginTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    public LineLoginTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 回呼帶入非法state_應被拒絕()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // 沒有經過 /line/login，因此沒有暫存的 state Cookie
        var response = await client.GetAsync("/api/auth/line/callback?code=fake-code&state=forged-state");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 回呼缺少授權碼_應被拒絕()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/auth/line/callback?state=some-state");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 無效的IdToken_應被拒絕()
    {
        var client = CreateLineLoginClient("這不是合法的 JWT");

        var exception = await Assert.ThrowsAsync<AppException>(
            () => client.ExchangeCodeAsync("code", "nonce", CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public async Task LINE未回傳IdToken_應被拒絕()
    {
        var client = CreateLineLoginClient(idToken: null);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => client.ExchangeCodeAsync("code", "nonce", CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public void 授權網址_應包含state與nonce()
    {
        var client = CreateLineLoginClient(idToken: null);

        var url = client.BuildAuthorizationUrl("state-abc", "nonce-xyz");

        Assert.Contains("state=state-abc", url);
        Assert.Contains("nonce=nonce-xyz", url);
        Assert.Contains("response_type=code", url);
    }

    private static LineLoginClient CreateLineLoginClient(string? idToken)
    {
        var options = Options.Create(new LineLoginOptions
        {
            ChannelId = "test-channel",
            ChannelSecret = "test-secret",
            CallbackUrl = "http://localhost:8080/api/auth/line/callback",
        });

        var httpClient = new HttpClient(new StubTokenEndpointHandler(idToken));

        // 使用空的 JWKS，任何 ID Token 的簽章都無法通過驗證
        var configurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
            new OpenIdConnectConfiguration());

        return new LineLoginClient(httpClient, options, configurationManager);
    }

    /// <summary>模擬 LINE Token 端點，回傳指定的 id_token。</summary>
    private class StubTokenEndpointHandler : HttpMessageHandler
    {
        private readonly string? _idToken;

        public StubTokenEndpointHandler(string? idToken)
        {
            _idToken = idToken;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = _idToken is null
                ? "{\"access_token\":\"token\"}"
                : $"{{\"access_token\":\"token\",\"id_token\":\"{_idToken}\"}}";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
