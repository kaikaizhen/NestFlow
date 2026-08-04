using System.Net;
using System.Net.Http.Json;
using static NestFlow_Backend.Tests.TestClientExtensions;

namespace NestFlow_Backend.Tests;

/// <summary>
/// Module 13：PWA 儲藏庫。涵蓋新增、修改、軟刪除、依名稱與位置搜尋、
/// 最近更新排序，以及 Workspace 隔離與家庭共用。
/// </summary>
public class StorageItemTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    public StorageItemTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 新增物品_應出現在列表()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("收納的人");

        var created = await CreateItemAsync(client, workspaceId, "電鑽", "客廳電視櫃第二層", "含充電器");

        var item = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Equal(created.Id, item.Id);
        Assert.Equal("電鑽", item.Name);
        Assert.Equal("客廳電視櫃第二層", item.Location);
        Assert.Equal("含充電器", item.Note);
        Assert.Equal("收納的人", item.CreatedByDisplayName);
    }

    [Fact]
    public async Task 修改物品_應更新名稱與位置()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("搬東西的人");
        var created = await CreateItemAsync(client, workspaceId, "護照", "主臥衣櫃", null);

        var response = await client.PutAsJsonAsync($"/api/storage-items/{created.Id}", new
        {
            workspaceId,
            name = "護照與印章",
            location = "主臥衣櫃保險箱",
            note = "第二格抽屜",
        });

        var updated = await response.Content.ReadFromJsonAsync<StorageItemResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("護照與印章", updated!.Name);
        Assert.Equal("主臥衣櫃保險箱", updated.Location);
        Assert.Equal("第二格抽屜", updated.Note);
    }

    [Fact]
    public async Task 可依物品名稱搜尋()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("找電鑽的人");

        await CreateItemAsync(client, workspaceId, "電鑽", "陽台工具箱", null);
        await CreateItemAsync(client, workspaceId, "護照", "主臥衣櫃", null);

        var found = Assert.Single(await ListAsync(client, workspaceId, "電鑽"));

        Assert.Equal("電鑽", found.Name);
    }

    [Fact]
    public async Task 可依存放位置搜尋()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("找陽台的人");

        await CreateItemAsync(client, workspaceId, "電鑽", "陽台工具箱", null);
        await CreateItemAsync(client, workspaceId, "螺絲起子", "陽台工具箱", null);
        await CreateItemAsync(client, workspaceId, "護照", "主臥衣櫃", null);

        var found = await ListAsync(client, workspaceId, "陽台");

        Assert.Equal(2, found.Count);
        Assert.DoesNotContain(found, x => x.Name == "護照");
    }

    [Fact]
    public async Task 搜尋不比對備註()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("備註測試的人");

        await CreateItemAsync(client, workspaceId, "電鑽", "陽台工具箱", "保固到二零二七年");

        // 計畫指定只依物品名稱與存放位置搜尋，備註不列入
        Assert.Empty(await ListAsync(client, workspaceId, "保固"));
    }

    [Fact]
    public async Task 搜尋無結果_應回傳空列表()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("找不到的人");

        await CreateItemAsync(client, workspaceId, "電鑽", "陽台工具箱", null);

        Assert.Empty(await ListAsync(client, workspaceId, "不存在的東西"));
    }

    [Fact]
    public async Task 列表_應依最後更新時間由新到舊排序()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("排序測試的人");

        var first = await CreateItemAsync(client, workspaceId, "先加的", "位置一", null);
        await CreateItemAsync(client, workspaceId, "後加的", "位置二", null);

        // 時鐘往前推，確保更新時間確實比建立時間晚
        _factory.Time.Offset = TimeSpan.FromMinutes(10);

        try
        {
            await client.PutAsJsonAsync($"/api/storage-items/{first.Id}", new
            {
                workspaceId,
                name = "先加的",
                location = "位置一之一",
            });

            var items = await ListAsync(client, workspaceId);

            // 剛更新過的那筆會浮到最上面
            Assert.Equal(["先加的", "後加的"], items.Select(x => x.Name));
        }
        finally
        {
            _factory.Time.Offset = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task 軟刪除後_不應出現在列表與搜尋()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("丟東西的人");
        var created = await CreateItemAsync(client, workspaceId, "舊電池", "抽屜", null);

        var deleted = await client.DeleteAsync($"/api/storage-items/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await ListAsync(client, workspaceId));
        Assert.Empty(await ListAsync(client, workspaceId, "舊電池"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/storage-items/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task 其他資料空間的物品_不應互相看到()
    {
        var (client, personalId) = await CreateUserWithWorkspaceAsync("隔離測試的人");
        var familyId = await client.CreateWorkspaceAsync("我們家", "family");

        await CreateItemAsync(client, personalId, "個人證件", "書桌抽屜", null);
        await CreateItemAsync(client, familyId, "家裡的工具", "陽台", null);

        Assert.Equal("個人證件", Assert.Single(await ListAsync(client, personalId)).Name);
        Assert.Equal("家裡的工具", Assert.Single(await ListAsync(client, familyId)).Name);
    }

    [Fact]
    public async Task 非成員_不可讀取或寫入該資料空間的物品()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("物品擁有者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var created = await CreateItemAsync(owner, familyId, "家裡的工具", "陽台", null);

        var outsider = _factory.CreateClient();
        await outsider.LoginAsync(NewSubject(), "外人");

        var read = await outsider.GetAsync(BuildListUrl(familyId));
        var readOne = await outsider.GetAsync($"/api/storage-items/{created.Id}");
        var write = await outsider.PostAsJsonAsync("/api/storage-items", new
        {
            workspaceId = familyId,
            name = "偷加的",
            location = "某處",
        });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, readOne.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task 家庭成員_可查看並修改其他成員建立的物品()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("家庭建立者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var code = await owner.CreateInvitationAsync(familyId);

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "家庭成員");
        (await member.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var created = await CreateItemAsync(owner, familyId, "備用鑰匙", "玄關抽屜", null);

        var seen = Assert.Single(await ListAsync(member, familyId));
        var response = await member.PutAsJsonAsync($"/api/storage-items/{created.Id}", new
        {
            workspaceId = familyId,
            name = "備用鑰匙",
            location = "玄關鞋櫃上層",
        });

        Assert.Equal("備用鑰匙", seen.Name);
        Assert.Equal("家庭建立者", seen.CreatedByDisplayName);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 空白名稱或位置_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("亂填的人");

        var blankName = await client.PostAsJsonAsync("/api/storage-items", new
        {
            workspaceId,
            name = "   ",
            location = "抽屜",
        });

        var blankLocation = await client.PostAsJsonAsync("/api/storage-items", new
        {
            workspaceId,
            name = "東西",
            location = "   ",
        });

        Assert.Equal(HttpStatusCode.BadRequest, blankName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blankLocation.StatusCode);
    }

    [Fact]
    public async Task 未登入_不可存取儲藏庫()
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

    private static async Task<StorageItemResponse> CreateItemAsync(
        HttpClient client,
        Guid workspaceId,
        string name,
        string location,
        string? note)
    {
        var response = await client.PostAsJsonAsync("/api/storage-items", new
        {
            workspaceId,
            name,
            location,
            note,
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<StorageItemResponse>())!;
    }

    private static async Task<List<StorageItemResponse>> ListAsync(
        HttpClient client,
        Guid workspaceId,
        string? keyword = null)
    {
        return (await client.GetFromJsonAsync<List<StorageItemResponse>>(
            BuildListUrl(workspaceId, keyword)))!;
    }

    private static string BuildListUrl(Guid workspaceId, string? keyword = null)
    {
        var url = $"/api/storage-items?workspaceId={workspaceId}";

        return keyword is null ? url : $"{url}&keyword={Uri.EscapeDataString(keyword)}";
    }

    private static string NewSubject() => $"U{Guid.NewGuid():N}";

    private record StorageItemResponse(
        Guid Id,
        string Name,
        string Location,
        string? Note,
        DateTimeOffset UpdatedAt,
        Guid CreatedByUserId,
        string CreatedByDisplayName);
}
