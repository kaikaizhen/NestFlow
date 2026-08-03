using System.Net;
using System.Net.Http.Json;
using static NestFlow_Backend.Tests.TestClientExtensions;

namespace NestFlow_Backend.Tests;

/// <summary>Module 6：提醒。</summary>
public class ReminderTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    public ReminderTests(NestFlowApiFactory factory)
    {
        _factory = factory;

        // 每個測試都從真實時間與乾淨的推播紀錄開始
        _factory.Time.Offset = TimeSpan.Zero;
        _factory.Messaging.Reset();
    }

    [Fact]
    public async Task 建立提醒_應出現在列表且為等待發送()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("排提醒的人");

        var created = await CreateReminderAsync(client, workspaceId, "繳水電費", TimeSpan.FromHours(2));

        var single = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Equal(created.Id, single.Id);
        Assert.Equal("繳水電費", single.Content);
        Assert.Equal("pending", single.Status);
        Assert.Equal(0, single.RetryCount);
        Assert.Equal("排提醒的人", single.CreatedByDisplayName);
    }

    [Fact]
    public async Task 取消提醒_不應出現在列表()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("取消提醒的人");
        var created = await CreateReminderAsync(client, workspaceId, "要取消的提醒", TimeSpan.FromHours(2));

        var response = await client.DeleteAsync($"/api/reminders/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ListAsync(client, workspaceId));
    }

    [Fact]
    public async Task 到期提醒_應以LINE推播並標記已發送()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("等通知的人");
        await CreateReminderAsync(client, workspaceId, "開會了", TimeSpan.FromMinutes(30));

        // 時間往前推到觸發時間之後
        _factory.Time.Offset = TimeSpan.FromHours(1);

        var result = await DispatchAsync(client);

        Assert.Equal(1, result.Claimed);
        Assert.Equal(1, result.Sent);

        var push = Assert.Single(_factory.Messaging.Pushes);
        Assert.Contains("開會了", push.Text);

        Assert.Equal("sent", Assert.Single(await ListAsync(client, workspaceId)).Status);
    }

    [Fact]
    public async Task 已成功提醒_不重複發送()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("只想被通知一次的人");
        await CreateReminderAsync(client, workspaceId, "只提醒一次", TimeSpan.FromMinutes(30));

        _factory.Time.Offset = TimeSpan.FromHours(1);

        var first = await DispatchAsync(client);
        var second = await DispatchAsync(client);

        Assert.Equal(1, first.Sent);

        // 第二次掃描已經取不到這筆
        Assert.Equal(0, second.Claimed);
        Assert.Equal(0, second.Sent);
        Assert.Single(_factory.Messaging.Pushes);
    }

    [Fact]
    public async Task 尚未到期的提醒_不應被發送()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("還沒到時間的人");
        await CreateReminderAsync(client, workspaceId, "還沒到", TimeSpan.FromHours(5));

        var result = await DispatchAsync(client);

        Assert.Equal(0, result.Claimed);
        Assert.Empty(_factory.Messaging.Pushes);
        Assert.Equal("pending", Assert.Single(await ListAsync(client, workspaceId)).Status);
    }

    [Fact]
    public async Task 已取消的提醒_不應被發送()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("取消後不想被吵的人");
        var created = await CreateReminderAsync(client, workspaceId, "已取消", TimeSpan.FromMinutes(30));

        (await client.DeleteAsync($"/api/reminders/{created.Id}")).EnsureSuccessStatusCode();

        _factory.Time.Offset = TimeSpan.FromHours(1);

        var result = await DispatchAsync(client);

        Assert.Equal(0, result.Claimed);
        Assert.Empty(_factory.Messaging.Pushes);
    }

    [Fact]
    public async Task 推播失敗_應重試並在達上限後標記失敗()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("推不出去的人");
        await CreateReminderAsync(client, workspaceId, "推播會失敗", TimeSpan.FromMinutes(30));

        _factory.Messaging.PushSucceeds = false;
        _factory.Time.Offset = TimeSpan.FromHours(1);

        // 前兩次退回 Pending 等待重試
        var first = await DispatchAsync(client);
        var second = await DispatchAsync(client);

        Assert.Equal(1, first.Retrying);
        Assert.Equal(1, second.Retrying);
        Assert.Equal("pending", Assert.Single(await ListAsync(client, workspaceId)).Status);

        // 第三次達到上限，轉為失敗不再重試
        var third = await DispatchAsync(client);
        var fourth = await DispatchAsync(client);

        Assert.Equal(1, third.Failed);
        Assert.Equal(0, fourth.Claimed);

        var reminder = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Equal("failed", reminder.Status);
        Assert.Equal(3, reminder.RetryCount);
    }

    [Fact]
    public async Task 已發送的提醒_不可取消()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("想反悔的人");
        var created = await CreateReminderAsync(client, workspaceId, "已經送出去了", TimeSpan.FromMinutes(30));

        _factory.Time.Offset = TimeSpan.FromHours(1);
        await DispatchAsync(client);

        var response = await client.DeleteAsync($"/api/reminders/{created.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 過去時間的提醒_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("填過去時間的人");

        var response = await client.PostAsJsonAsync("/api/reminders", new
        {
            workspaceId,
            content = "來不及了",
            triggerAt = DateTimeOffset.UtcNow.AddHours(-1),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 其他資料空間的提醒_不應互相看到()
    {
        var (client, personalId) = await CreateUserWithWorkspaceAsync("隔離測試者");
        var familyId = await client.CreateWorkspaceAsync("我們家", "family");

        await CreateReminderAsync(client, personalId, "個人提醒", TimeSpan.FromHours(2));
        await CreateReminderAsync(client, familyId, "家庭提醒", TimeSpan.FromHours(2));

        Assert.Equal("個人提醒", Assert.Single(await ListAsync(client, personalId)).Content);
        Assert.Equal("家庭提醒", Assert.Single(await ListAsync(client, familyId)).Content);
    }

    [Fact]
    public async Task 非成員_不可讀取或建立該資料空間的提醒()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("空間擁有者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var created = await CreateReminderAsync(owner, familyId, "家庭提醒", TimeSpan.FromHours(2));

        var outsider = _factory.CreateClient();
        await outsider.LoginAsync(NewSubject(), "外人");

        var read = await outsider.GetAsync(BuildListUrl(familyId));
        var cancel = await outsider.DeleteAsync($"/api/reminders/{created.Id}");
        var write = await outsider.PostAsJsonAsync("/api/reminders", new
        {
            workspaceId = familyId,
            content = "偷加的提醒",
            triggerAt = DateTimeOffset.UtcNow.AddHours(2),
        });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task 未登入_不可存取提醒()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(BuildListUrl(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------
    // 測試輔助
    // -----------------------------------------------------------
    /// <summary>
    /// 建立使用者並完成 LINE 官方帳號綁定。
    /// 沒有綁定就沒有推播對象，提醒會一路重試到失敗，因此提醒測試一律先綁定。
    /// </summary>
    private async Task<(HttpClient Client, Guid PersonalWorkspaceId)> CreateUserWithWorkspaceAsync(string name)
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), name);

        var codeResponse = await client.PostAsync("/api/auth/me/binding-code", null);
        codeResponse.EnsureSuccessStatusCode();
        var code = (await codeResponse.Content.ReadFromJsonAsync<InvitationResponse>())!.Code;

        var bind = await client.PostAsJsonAsync(
            "/api/dev/line/message",
            new { externalUserId = NewSubject(), text = code });
        bind.EnsureSuccessStatusCode();

        var workspaces = await client.GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");

        return (client, workspaces!.Single().Id);
    }

    private static async Task<ReminderResponse> CreateReminderAsync(
        HttpClient client,
        Guid workspaceId,
        string content,
        TimeSpan leadTime)
    {
        var response = await client.PostAsJsonAsync("/api/reminders", new
        {
            workspaceId,
            content,
            triggerAt = DateTimeOffset.UtcNow.Add(leadTime),
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ReminderResponse>())!;
    }

    private static async Task<DispatchResponse> DispatchAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/dev/reminders/dispatch", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<DispatchResponse>())!;
    }

    private static async Task<List<ReminderResponse>> ListAsync(HttpClient client, Guid workspaceId)
    {
        return (await client.GetFromJsonAsync<List<ReminderResponse>>(BuildListUrl(workspaceId)))!;
    }

    private static string BuildListUrl(Guid workspaceId)
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(30).ToString("O"));

        return $"/api/reminders?workspaceId={workspaceId}&from={from}&to={to}";
    }

    private static string NewSubject() => $"U{Guid.NewGuid():N}";

    private record ReminderResponse(
        Guid Id,
        string Content,
        DateTimeOffset TriggerAt,
        string Status,
        int RetryCount,
        Guid CreatedByUserId,
        string CreatedByDisplayName);

    private record DispatchResponse(int Claimed, int Sent, int Retrying, int Failed);
}
