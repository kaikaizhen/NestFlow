using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NestFlow_Backend.Tests;

/// <summary>驗證 Backend 一律使用 Library 的 RFC 9457 錯誤格式。</summary>
public class ErrorHandlingTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    public ErrorHandlingTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 未登入的受保護端點_回傳LibraryProblemDetails()
    {
        var response = await _factory.CreateClient().GetAsync("/api/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("尚未登入。", document.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task 輸入模型驗證失敗_回傳ProblemDetails與欄位錯誤()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/dev/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("驗證失敗", document.RootElement.GetProperty("title").GetString());
        Assert.True(document.RootElement.GetProperty("errors").EnumerateObject().Any());
    }
}
