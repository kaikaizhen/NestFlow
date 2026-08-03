using System.Net;

namespace NestFlow_Backend.Tests;

public class HealthCheckTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    public HealthCheckTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 存活探針_應回傳200且狀態為Healthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body);
    }

    [Fact]
    public async Task 未定義端點_應回傳404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/not-exists");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
