using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static NestFlow_Backend.Tests.TestClientExtensions;

namespace NestFlow_Backend.Tests;

/// <summary>
/// Module 4：LINE 固定格式記帳。
/// 大部分流程以 /api/dev/line 模擬端點驗證，簽章與事件解析則直接打 Webhook。
/// </summary>
public class LineWebhookTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    public LineWebhookTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 固定格式記帳_確認後才寫入資料()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("記帳的人");

        var pending = await SendAsync(client, lineUserId, "記帳 午餐 120");

        // 只產生待確認，尚未寫入
        Assert.Contains("準備新增支出", pending);
        Assert.Contains("120", pending);
        Assert.Empty(await ListAsync(client, workspaceId));

        var confirmed = await SendAsync(client, lineUserId, "確認");

        var entry = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Contains("已記錄支出", confirmed);
        Assert.Equal("expense", entry.Type);
        Assert.Equal(120m, entry.Amount);
        Assert.Equal("food", entry.Category);
    }

    [Fact]
    public async Task 收入格式_應解析為收入並寫入()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("領薪水的人");

        await SendAsync(client, lineUserId, "收入 薪資 50000");
        await SendAsync(client, lineUserId, "確認");

        var entry = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Equal("income", entry.Type);
        Assert.Equal(50000m, entry.Amount);
        Assert.Equal("salary", entry.Category);
    }

    [Fact]
    public async Task 備註格式_應保留備註()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("搭捷運的人");

        await SendAsync(client, lineUserId, "支出 交通 60 捷運");
        await SendAsync(client, lineUserId, "確認");

        var entry = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Equal("transport", entry.Category);
        Assert.Equal("捷運", entry.Note);
    }

    [Fact]
    public async Task 取消_不應寫入任何資料()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("反悔的人");

        await SendAsync(client, lineUserId, "記帳 午餐 120");
        var reply = await SendAsync(client, lineUserId, "取消");

        Assert.Contains("已取消", reply);
        Assert.Empty(await ListAsync(client, workspaceId));

        // 取消後再確認也不會寫入
        var again = await SendAsync(client, lineUserId, "確認");

        Assert.Contains("沒有待確認", again);
        Assert.Empty(await ListAsync(client, workspaceId));
    }

    [Fact]
    public async Task 相同事件重送_只處理一次()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("被重送的人");

        const string eventId = "evt-duplicated-001";

        var first = await SendAsync(client, lineUserId, "記帳 午餐 120", eventId);
        var second = await SendAsync(client, lineUserId, "確認", eventId);

        Assert.Contains("準備新增支出", first);

        // 第二次是重送事件，直接忽略且不回覆
        Assert.Null(second);
        Assert.Empty(await ListAsync(client, workspaceId));
    }

    [Fact]
    public async Task 無法解析的訊息_應回覆用法且不建立待確認()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("亂打字的人");

        var reply = await SendAsync(client, lineUserId, "今天天氣真好");

        Assert.Contains("可以這樣記帳", reply);

        // 沒有待確認可確認
        Assert.Contains("沒有待確認", await SendAsync(client, lineUserId, "確認"));
        Assert.Empty(await ListAsync(client, workspaceId));
    }

    [Fact]
    public async Task 新的記帳_應取代前一筆待確認()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("改主意的人");

        await SendAsync(client, lineUserId, "記帳 午餐 120");
        var second = await SendAsync(client, lineUserId, "支出 交通 60 捷運");
        await SendAsync(client, lineUserId, "確認");

        var entry = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Contains("上一筆待確認的記帳已取消", second);
        Assert.Equal(60m, entry.Amount);
    }

    [Fact]
    public async Task 未綁定的使用者_可用綁定碼完成綁定()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "要綁定的人");

        // 另一個 LINE 帳號（例如 Messaging 與 Login 不同 Provider）尚未對應到任何使用者
        var messagingUserId = NewSubject();

        var before = await SendAsync(client, messagingUserId, "記帳 午餐 120");
        Assert.Contains("還沒有綁定", before);

        Assert.False(await IsMessagingLinkedAsync(client));

        var code = await CreateBindingCodeAsync(client);

        Assert.Contains("綁定成功", await SendAsync(client, messagingUserId, code));

        // 綁定後設定頁會顯示已綁定，且可直接記帳
        Assert.True(await IsMessagingLinkedAsync(client));
        Assert.Contains("準備新增支出", await SendAsync(client, messagingUserId, "記帳 午餐 120"));
    }

    [Fact]
    public async Task 綁定碼只能使用一次()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "重複使用綁定碼的人");

        var code = await CreateBindingCodeAsync(client);

        Assert.Contains("綁定成功", await SendAsync(client, NewSubject(), code));
        Assert.Contains("無效或已被使用", await SendAsync(client, NewSubject(), code));
    }

    [Fact]
    public async Task Webhook_簽章錯誤_應拒絕()
    {
        var client = _factory.CreateClient();
        var body = BuildWebhookBody("evt-bad-signature", NewSubject(), "記帳 午餐 120");

        var missing = await PostWebhookAsync(client, body, signature: null);
        var wrong = await PostWebhookAsync(client, body, signature: Sign(body, "wrong-secret"));

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Fact]
    public async Task Webhook_簽章正確_應處理事件並回200()
    {
        var (client, lineUserId, workspaceId) = await CreateBoundUserAsync("走正式流程的人");

        var body = BuildWebhookBody("evt-signed-001", lineUserId, "記帳 午餐 120");

        var response = await PostWebhookAsync(client, body, Sign(body, NestFlowApiFactory.TestChannelSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 事件已被處理：待確認存在，確認後即寫入
        await SendAsync(client, lineUserId, "確認");

        Assert.Equal(120m, Assert.Single(await ListAsync(client, workspaceId)).Amount);
    }

    // -----------------------------------------------------------
    // 測試輔助
    // -----------------------------------------------------------

    /// <summary>
    /// 建立一位已登入的使用者。Messaging 與 Login 屬同一 Provider，
    /// 因此相同的 LINE 使用者 ID 不需另外綁定即可對應到該帳號。
    /// </summary>
    private async Task<(HttpClient Client, string LineUserId, Guid WorkspaceId)> CreateBoundUserAsync(string name)
    {
        var client = _factory.CreateClient();
        var lineUserId = NewSubject();

        await client.LoginAsync(lineUserId, name);

        var workspaces = await client.GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");

        return (client, lineUserId, workspaces!.Single().Id);
    }

    private static async Task<string?> SendAsync(
        HttpClient client,
        string externalUserId,
        string text,
        string? eventId = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/dev/line/message",
            new { externalUserId, text, eventId });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SimulateResponse>())!.Reply;
    }

    private static async Task<string> CreateBindingCodeAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/auth/me/binding-code", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<InvitationResponse>())!.Code;
    }

    private static async Task<bool> IsMessagingLinkedAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");

        return me!.IsLineMessagingLinked;
    }

    private static Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, byte[] body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/line")
        {
            Content = new ByteArrayContent(body),
        };

        request.Content.Headers.ContentType = new("application/json");

        if (signature is not null)
        {
            request.Headers.Add("X-Line-Signature", signature);
        }

        return client.SendAsync(request);
    }

    private static byte[] BuildWebhookBody(string eventId, string externalUserId, string text)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            destination = "Ufake",
            events = new[]
            {
                new
                {
                    type = "message",
                    webhookEventId = eventId,
                    replyToken = "reply-token",
                    source = new { type = "user", userId = externalUserId },
                    message = new { type = "text", text },
                },
            },
        });
    }

    private static string Sign(byte[] body, string channelSecret)
    {
        return Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(channelSecret), body));
    }

    private static async Task<List<EntryResponse>> ListAsync(HttpClient client, Guid workspaceId)
    {
        // 待確認記帳以當下時間為發生時間，這裡取寬鬆區間避免受執行時間影響
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

        return (await client.GetFromJsonAsync<List<EntryResponse>>(
            $"/api/account-entries?workspaceId={workspaceId}&from={from}&to={to}"))!;
    }

    private static string NewSubject() => $"U{Guid.NewGuid():N}";

    private record SimulateResponse(string? Reply);

    private record MeResponse(bool IsLineMessagingLinked, bool IsLineMessagingConfigured);

    private record EntryResponse(
        Guid Id,
        string Type,
        decimal Amount,
        string Currency,
        string Category,
        string? Note,
        DateTimeOffset OccurredAt);
}
