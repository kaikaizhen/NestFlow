using System.Net.Http.Json;
using NestFlow_Backend.Services.External;
using static NestFlow_Backend.Tests.TestClientExtensions;

namespace NestFlow_Backend.Tests;

/// <summary>
/// 固定格式解析不出結果時呼叫 Dify Workflow 的後援流程。
/// 每個測試都自行設定 FakeDifyClient.NextResult，涵蓋整個測試類別共用同一個 Factory 執行個體。
/// </summary>
public class DifyFallbackTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    public DifyFallbackTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dify解析為記帳_確認後才寫入資料()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("用自然語言記帳的人");

        _factory.Dify.NextResult = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = 180,
            Category = "food",
            Note = "跟同學吃牛肉麵",
            Confidence = 0.98m,
        };

        var pending = await SendAsync(client, lineUserId, "今天午餐和同學吃牛肉麵花了 180 元");

        Assert.Contains("準備新增支出", pending);
        Assert.Contains("180", pending);
        Assert.Empty(await ListAsync(client, workspaceId));

        var confirmed = await SendAsync(client, lineUserId, "確認");

        var entry = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Contains("已記錄支出", confirmed);
        Assert.Equal(180m, entry.Amount);
        Assert.Equal("food", entry.Category);

        // Dify 呼叫帶入的訊息應該是原始文字，而非固定格式
        Assert.Equal("今天午餐和同學吃牛肉麵花了 180 元", _factory.Dify.LastRequest!.Message);
    }

    [Fact]
    public async Task Dify解析為行程_確認後才寫入行事曆()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("用自然語言排行程的人");

        _factory.Dify.NextResult = new DifyCommandResult
        {
            Kind = "event",
            Confidence = 0.97m,
            Event = new DifyEventResult
            {
                Title = "跟教授討論論文",
                DayOffset = 1,
                StartHour = 14,
                StartMinute = 0,
                EndHour = 15,
                EndMinute = 30,
            },
        };

        var pending = await SendAsync(client, lineUserId, "明天下午兩點跟教授討論論文到三點半");

        Assert.Contains("準備新增行程", pending);
        Assert.Contains("跟教授討論論文", pending);

        await SendAsync(client, lineUserId, "確認");

        var created = Assert.Single(await ListEventsAsync(client, workspaceId));

        Assert.Equal("跟教授討論論文", created.Title);
        Assert.Equal(TimeSpan.FromMinutes(90), created.EndAt - created.StartAt);
    }

    [Fact]
    public async Task Dify回傳未知分類代碼時_不應信任_應歸為其他且標註推測()
    {
        var (client, lineUserId, _) = await CreateBoundUserAsync("分類亂猜的人");

        _factory.Dify.NextResult = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = 50,
            Category = "not-a-real-category",
            Confidence = 0.9m,
        };

        var pending = await SendAsync(client, lineUserId, "買了個東西 50 元");

        Assert.Contains("找不到對應分類，已歸為其他", pending);
    }

    [Fact]
    public async Task Dify回傳none時_應回覆用法且不建立待確認()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("聊天的人");

        _factory.Dify.NextResult = new DifyCommandResult { Kind = "none", Confidence = 0.2m };

        var reply = await SendAsync(client, lineUserId, "今天天氣真好");

        Assert.Contains("可以這樣記帳", reply);
        Assert.Empty(await ListAsync(client, workspaceId));
    }

    [Fact]
    public async Task Dify信心值過低時_應安全退回用法提示()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("信心不足的人");

        _factory.Dify.NextResult = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = 100,
            Category = "food",
            Confidence = 0.1m,
        };

        var reply = await SendAsync(client, lineUserId, "隨便打的訊息");

        Assert.Contains("可以這樣記帳", reply);
        Assert.Empty(await ListAsync(client, workspaceId));
    }

    [Fact]
    public async Task Dify呼叫失敗時_應安全退回用法提示()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("Dify掛掉時的人");

        _factory.Dify.NextResult = null;

        var reply = await SendAsync(client, lineUserId, "不知道在打什麼");

        Assert.Contains("可以這樣記帳", reply);
        Assert.Empty(await ListAsync(client, workspaceId));
    }

    // -----------------------------------------------------------
    // 測試輔助（與 LineWebhookTests 相同模式，各測試檔獨立避免共用私有方法）
    // -----------------------------------------------------------
    private async Task<(HttpClient Client, string LineUserId, Guid WorkspaceId)> CreateBoundUserAsync(string name)
    {
        var client = _factory.CreateClient();
        var lineUserId = $"U{Guid.NewGuid():N}";

        await client.LoginAsync(lineUserId, name);

        var workspaces = await client.GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");

        return (client, lineUserId, workspaces!.Single().Id);
    }

    private static async Task<string?> SendAsync(HttpClient client, string externalUserId, string text)
    {
        var response = await client.PostAsJsonAsync(
            "/api/dev/line/message",
            new { externalUserId, text, eventId = (string?)null });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SimulateResponse>())!.Reply;
    }

    private static async Task<List<EntryResponse>> ListAsync(HttpClient client, Guid workspaceId)
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

        return (await client.GetFromJsonAsync<List<EntryResponse>>(
            $"/api/account-entries?workspaceId={workspaceId}&from={from}&to={to}"))!;
    }

    private static async Task<List<EventResponse>> ListEventsAsync(HttpClient client, Guid workspaceId)
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(10).ToString("O"));

        return (await client.GetFromJsonAsync<List<EventResponse>>(
            $"/api/calendar-events?workspaceId={workspaceId}&from={from}&to={to}"))!;
    }

    private record SimulateResponse(string? Reply);

    private record EventResponse(
        Guid Id,
        string Title,
        string? Description,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt);

    private record EntryResponse(
        Guid Id,
        string Type,
        decimal Amount,
        string Currency,
        string Category,
        string? Note,
        DateTimeOffset OccurredAt);
}
