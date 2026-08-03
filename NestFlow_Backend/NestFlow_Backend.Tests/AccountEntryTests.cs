using System.Net;
using System.Net.Http.Json;
using static NestFlow_Backend.Tests.TestClientExtensions;

namespace NestFlow_Backend.Tests;

public class AccountEntryTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    /// <summary>測試用的月份區間，模擬前端以 Asia/Taipei 換算後的 UTC 起訖。</summary>
    private static readonly DateTimeOffset MonthFrom = new(2026, 7, 31, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MonthTo = new(2026, 8, 31, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InMonth = new(2026, 8, 10, 4, 0, 0, TimeSpan.Zero);

    public AccountEntryTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 新增記帳_應可在列表與統計中看到()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("記帳者");

        var created = await CreateEntryAsync(client, workspaceId, "expense", 120, "food", "午餐");

        var entries = await ListAsync(client, workspaceId);
        var summary = await SummaryAsync(client, workspaceId);

        Assert.Equal(created.Id, Assert.Single(entries).Id);
        Assert.Equal(120m, entries[0].Amount);
        Assert.Equal("food", entries[0].Category);
        Assert.Equal("記帳者", entries[0].CreatedByDisplayName);

        var twd = Assert.Single(summary);
        Assert.Equal("TWD", twd.Currency);
        Assert.Equal(0m, twd.Income);
        Assert.Equal(120m, twd.Expense);
        Assert.Equal(-120m, twd.Balance);
    }

    [Fact]
    public async Task 收支統計_應算出正確結餘()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("結餘測試者");

        await CreateEntryAsync(client, workspaceId, "income", 50000, "salary", "月薪");
        await CreateEntryAsync(client, workspaceId, "expense", 1250, "shopping", "超市");
        await CreateEntryAsync(client, workspaceId, "expense", 320.55m, "food", "晚餐");

        var twd = Assert.Single(await SummaryAsync(client, workspaceId));

        Assert.Equal(50000m, twd.Income);
        Assert.Equal(1570.55m, twd.Expense);
        Assert.Equal(48429.45m, twd.Balance);
    }

    [Fact]
    public async Task 不同幣別_應分開加總()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("多幣別使用者");

        await CreateEntryAsync(client, workspaceId, "expense", 100, "food", null, currency: "TWD");
        await CreateEntryAsync(client, workspaceId, "expense", 20, "food", null, currency: "USD");
        await CreateEntryAsync(client, workspaceId, "income", 50, "bonus", null, currency: "USD");

        var summary = await SummaryAsync(client, workspaceId);

        Assert.Equal(2, summary.Count);

        var usd = summary.Single(x => x.Currency == "USD");
        Assert.Equal(50m, usd.Income);
        Assert.Equal(20m, usd.Expense);
        Assert.Equal(30m, usd.Balance);

        var twd = summary.Single(x => x.Currency == "TWD");
        Assert.Equal(100m, twd.Expense);
    }

    [Fact]
    public async Task 取得單筆記帳_非成員應回404()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("記帳擁有者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var created = await CreateEntryAsync(owner, familyId, "expense", 100, "food", "私人午餐");

        var mine = await owner.GetFromJsonAsync<EntryResponse>($"/api/account-entries/{created.Id}");

        var outsider = _factory.CreateClient();
        await outsider.LoginAsync(NewSubject(), "外人");
        var theirs = await outsider.GetAsync($"/api/account-entries/{created.Id}");

        Assert.Equal("私人午餐", mine!.Note);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
    }

    [Fact]
    public async Task 修改記帳_應更新金額與分類()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("修改者");
        var created = await CreateEntryAsync(client, workspaceId, "expense", 120, "food", "午餐");

        var response = await client.PutAsJsonAsync(
            $"/api/account-entries/{created.Id}",
            new
            {
                workspaceId,
                type = "expense",
                amount = 180,
                currency = "TWD",
                category = "transport",
                note = "計程車",
                occurredAt = InMonth,
            });

        var updated = await response.Content.ReadFromJsonAsync<EntryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(180m, updated!.Amount);
        Assert.Equal("transport", updated.Category);
        Assert.Equal("計程車", updated.Note);
    }

    [Fact]
    public async Task 軟刪除後_不應出現在列表與統計()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("刪除者");
        var created = await CreateEntryAsync(client, workspaceId, "expense", 500, "home", null);

        var deleted = await client.DeleteAsync($"/api/account-entries/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await ListAsync(client, workspaceId));

        var twd = Assert.Single(await SummaryAsync(client, workspaceId));
        Assert.Equal(0m, twd.Expense);
    }

    [Fact]
    public async Task 區間外的記帳_不應計入()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("跨月使用者");

        // 台灣時間 8/1 00:30，UTC 為 7/31 16:30，仍屬 8 月
        await CreateEntryAsync(client, workspaceId, "expense", 10, "food", null,
            occurredAt: new DateTimeOffset(2026, 7, 31, 16, 30, 0, TimeSpan.Zero));

        // 台灣時間 7/31 23:30，UTC 為 7/31 15:30，屬 7 月
        await CreateEntryAsync(client, workspaceId, "expense", 999, "food", null,
            occurredAt: new DateTimeOffset(2026, 7, 31, 15, 30, 0, TimeSpan.Zero));

        var entries = await ListAsync(client, workspaceId);
        var twd = Assert.Single(await SummaryAsync(client, workspaceId));

        Assert.Single(entries);
        Assert.Equal(10m, twd.Expense);
    }

    [Fact]
    public async Task 其他資料空間的記帳_不應互相看到()
    {
        var (client, personalId) = await CreateUserWithWorkspaceAsync("隔離測試者");
        var familyId = await client.CreateWorkspaceAsync("我們家", "family");

        await CreateEntryAsync(client, personalId, "expense", 100, "food", "個人午餐");
        await CreateEntryAsync(client, familyId, "expense", 800, "shopping", "家庭採買");

        var personalEntries = await ListAsync(client, personalId);
        var familyEntries = await ListAsync(client, familyId);

        Assert.Equal("個人午餐", Assert.Single(personalEntries).Note);
        Assert.Equal("家庭採買", Assert.Single(familyEntries).Note);
    }

    [Fact]
    public async Task 非成員_不可讀取或寫入該資料空間的記帳()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("空間擁有者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        await CreateEntryAsync(owner, familyId, "expense", 100, "food", null);

        var outsider = _factory.CreateClient();
        await outsider.LoginAsync(NewSubject(), "外人");

        var read = await outsider.GetAsync(BuildListUrl(familyId));
        var write = await outsider.PostAsJsonAsync("/api/account-entries", new
        {
            workspaceId = familyId,
            type = "expense",
            amount = 1,
            currency = "TWD",
            category = "food",
            occurredAt = InMonth,
        });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task 家庭成員_可修改其他成員建立的記帳()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("家庭建立者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var code = await owner.CreateInvitationAsync(familyId);

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "家庭成員");
        (await member.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var created = await CreateEntryAsync(owner, familyId, "expense", 100, "food", "建立者記的");

        var response = await member.PutAsJsonAsync($"/api/account-entries/{created.Id}", new
        {
            workspaceId = familyId,
            type = "expense",
            amount = 150,
            currency = "TWD",
            category = "food",
            note = "成員改的",
            occurredAt = InMonth,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 分類與類型不符_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("亂填分類者");

        // salary 屬於收入分類，不可用於支出
        var response = await client.PostAsJsonAsync("/api/account-entries", new
        {
            workspaceId,
            type = "expense",
            amount = 100,
            currency = "TWD",
            category = "salary",
            occurredAt = InMonth,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 金額為零或負數_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("零元使用者");

        var zero = await client.PostAsJsonAsync("/api/account-entries", new
        {
            workspaceId,
            type = "expense",
            amount = 0,
            currency = "TWD",
            category = "food",
            occurredAt = InMonth,
        });

        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
    }

    [Fact]
    public async Task 不支援的幣別_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("幣別測試者");

        var response = await client.PostAsJsonAsync("/api/account-entries", new
        {
            workspaceId,
            type = "expense",
            amount = 100,
            currency = "XYZ",
            category = "food",
            occurredAt = InMonth,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 分類清單_應包含收入與支出分類()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "看分類的人");

        var categories = await client.GetFromJsonAsync<List<CategoryResponse>>("/api/account-entries/categories");

        Assert.Contains(categories!, x => x.Code == "food" && x.Type == "expense");
        Assert.Contains(categories!, x => x.Code == "salary" && x.Type == "income");
    }

    [Fact]
    public async Task 可更新使用者時區()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "換時區的人");

        var before = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal("Asia/Taipei", before!.TimeZone);

        var response = await client.PutAsJsonAsync("/api/auth/me/timezone", new { timeZone = "Asia/Tokyo" });
        var after = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Asia/Tokyo", after!.TimeZone);
    }

    [Fact]
    public async Task 無效時區_應被拒絕()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "亂填時區的人");

        var response = await client.PutAsJsonAsync("/api/auth/me/timezone", new { timeZone = "Mars/Olympus" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 未登入_不可存取記帳()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(BuildListUrl(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------
    // 測試輔助
    // -----------------------------------------------------------
    private async Task<(HttpClient Client, Guid PersonalWorkspaceId)> CreateUserWithWorkspaceAsync(string name)
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), name);

        var workspaces = await client.GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");

        return (client, workspaces!.Single().Id);
    }

    private static async Task<EntryResponse> CreateEntryAsync(
        HttpClient client,
        Guid workspaceId,
        string type,
        decimal amount,
        string category,
        string? note,
        string currency = "TWD",
        DateTimeOffset? occurredAt = null)
    {
        var response = await client.PostAsJsonAsync("/api/account-entries", new
        {
            workspaceId,
            type,
            amount,
            currency,
            category,
            note,
            occurredAt = occurredAt ?? InMonth,
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<EntryResponse>())!;
    }

    private static async Task<List<EntryResponse>> ListAsync(HttpClient client, Guid workspaceId)
    {
        return (await client.GetFromJsonAsync<List<EntryResponse>>(BuildListUrl(workspaceId)))!;
    }

    private static async Task<List<SummaryResponse>> SummaryAsync(HttpClient client, Guid workspaceId)
    {
        return (await client.GetFromJsonAsync<List<SummaryResponse>>(
            $"/api/account-entries/summary?workspaceId={workspaceId}&from={Encode(MonthFrom)}&to={Encode(MonthTo)}"))!;
    }

    private static string BuildListUrl(Guid workspaceId)
    {
        return $"/api/account-entries?workspaceId={workspaceId}&from={Encode(MonthFrom)}&to={Encode(MonthTo)}";
    }

    private static string Encode(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));

    private static string NewSubject() => $"U{Guid.NewGuid():N}";

    private record EntryResponse(
        Guid Id,
        string Type,
        decimal Amount,
        string Currency,
        string Category,
        string? Note,
        DateTimeOffset OccurredAt,
        Guid CreatedByUserId,
        string CreatedByDisplayName);

    private record SummaryResponse(string Currency, decimal Income, decimal Expense, decimal Balance);

    private record CategoryResponse(string Code, string Label, string Type);
}
