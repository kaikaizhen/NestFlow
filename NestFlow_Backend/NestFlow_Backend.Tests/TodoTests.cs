using System.Net;
using System.Net.Http.Json;
using static NestFlow_Backend.Tests.TestClientExtensions;

namespace NestFlow_Backend.Tests;

/// <summary>
/// Module 12：PWA 代辦。涵蓋一般代辦與購物清單兩種類型、完成與取消完成、
/// 軟刪除，以及 Workspace 隔離與家庭共用。
/// </summary>
public class TodoTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    private static readonly DateTimeOffset DueSoon = new(2026, 8, 10, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DueLater = new(2026, 8, 20, 4, 0, 0, TimeSpan.Zero);

    public TodoTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 新增代辦_應出現在未完成列表()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("建立代辦的人");

        var created = await CreateTodoAsync(client, workspaceId, "general", "繳電費", dueAt: DueSoon);

        var todo = Assert.Single(await ListAsync(client, workspaceId, "general"));

        Assert.Equal(created.Id, todo.Id);
        Assert.Equal("繳電費", todo.Title);
        Assert.Equal(DueSoon, todo.DueAt);
        Assert.False(todo.IsCompleted);
        Assert.Null(todo.CompletedAt);
        Assert.Equal("建立代辦的人", todo.CreatedByDisplayName);
    }

    [Fact]
    public async Task 修改代辦_應更新內容與到期時間()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("修改代辦的人");
        var created = await CreateTodoAsync(client, workspaceId, "general", "繳電費", dueAt: DueSoon);

        var response = await client.PutAsJsonAsync($"/api/todos/{created.Id}", new
        {
            workspaceId,
            type = "general",
            title = "繳水費",
            dueAt = DueLater,
        });

        var updated = await response.Content.ReadFromJsonAsync<TodoResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("繳水費", updated!.Title);
        Assert.Equal(DueLater, updated.DueAt);
    }

    [Fact]
    public async Task 標記完成_應移出未完成並記錄完成時間()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("完成代辦的人");
        var created = await CreateTodoAsync(client, workspaceId, "general", "倒垃圾");

        var completed = await SetCompletionAsync(client, created.Id, true);

        Assert.True(completed.IsCompleted);
        Assert.NotNull(completed.CompletedAt);

        // 只查未完成時不應再出現
        Assert.Empty(await ListAsync(client, workspaceId, "general", completed: false));
        Assert.Single(await ListAsync(client, workspaceId, "general", completed: true));
    }

    [Fact]
    public async Task 已完成代辦_可恢復為未完成()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("反悔的人");
        var created = await CreateTodoAsync(client, workspaceId, "general", "運動");

        await SetCompletionAsync(client, created.Id, true);
        var restored = await SetCompletionAsync(client, created.Id, false);

        Assert.False(restored.IsCompleted);
        Assert.Null(restored.CompletedAt);
        Assert.Single(await ListAsync(client, workspaceId, "general", completed: false));
        Assert.Empty(await ListAsync(client, workspaceId, "general", completed: true));
    }

    [Fact]
    public async Task 重複標記完成_不應改變原本的完成時間()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("重複點擊的人");
        var created = await CreateTodoAsync(client, workspaceId, "general", "整理房間");

        var first = await SetCompletionAsync(client, created.Id, true);

        // 時鐘往前推，若完成時間被覆寫就會看出差異
        _factory.Time.Offset = TimeSpan.FromMinutes(30);

        try
        {
            var second = await SetCompletionAsync(client, created.Id, true);

            Assert.Equal(first.CompletedAt, second.CompletedAt);
        }
        finally
        {
            _factory.Time.Offset = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task 購物清單項目_應保存數量()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("採買的人");

        await CreateTodoAsync(client, workspaceId, "shopping", "牛奶", quantity: 2);

        var item = Assert.Single(await ListAsync(client, workspaceId, "shopping"));

        Assert.Equal("牛奶", item.Title);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public async Task 一般代辦_不應保存數量()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("填錯數量的人");

        // 一般代辦沒有數量概念，即使前端送來也應忽略
        var created = await CreateTodoAsync(client, workspaceId, "general", "預約牙醫", quantity: 5);

        Assert.Null(created.Quantity);
    }

    [Fact]
    public async Task 從購物清單改為一般代辦_數量應被清除()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("改類型的人");
        var created = await CreateTodoAsync(client, workspaceId, "shopping", "衛生紙", quantity: 3);

        var response = await client.PutAsJsonAsync($"/api/todos/{created.Id}", new
        {
            workspaceId,
            type = "general",
            title = "衛生紙",
        });

        var updated = await response.Content.ReadFromJsonAsync<TodoResponse>();

        Assert.Equal("general", updated!.Type);
        Assert.Null(updated.Quantity);
    }

    [Fact]
    public async Task 兩種類型_應分開列出()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("兩種都用的人");

        await CreateTodoAsync(client, workspaceId, "general", "繳電費");
        await CreateTodoAsync(client, workspaceId, "shopping", "雞蛋", quantity: 1);

        var general = await ListAsync(client, workspaceId, "general");
        var shopping = await ListAsync(client, workspaceId, "shopping");

        Assert.Equal("繳電費", Assert.Single(general).Title);
        Assert.Equal("雞蛋", Assert.Single(shopping).Title);
    }

    [Fact]
    public async Task 未完成列表_應依到期時間排序且未設到期者在後()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("排序測試的人");

        await CreateTodoAsync(client, workspaceId, "general", "沒有期限");
        await CreateTodoAsync(client, workspaceId, "general", "比較晚到期", dueAt: DueLater);
        await CreateTodoAsync(client, workspaceId, "general", "最快到期", dueAt: DueSoon);

        var todos = await ListAsync(client, workspaceId, "general");

        Assert.Equal(["最快到期", "比較晚到期", "沒有期限"], todos.Select(x => x.Title));
    }

    [Fact]
    public async Task 未指定完成狀態時_未完成應排在已完成之前()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("混合列表的人");

        var done = await CreateTodoAsync(client, workspaceId, "general", "已經做完的");
        await CreateTodoAsync(client, workspaceId, "general", "還沒做的");
        await SetCompletionAsync(client, done.Id, true);

        var todos = await ListAsync(client, workspaceId, "general");

        Assert.Equal(["還沒做的", "已經做完的"], todos.Select(x => x.Title));
    }

    [Fact]
    public async Task 軟刪除後_不應出現在列表()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("刪除代辦的人");
        var created = await CreateTodoAsync(client, workspaceId, "general", "取消的事");

        var deleted = await client.DeleteAsync($"/api/todos/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await ListAsync(client, workspaceId, "general"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/todos/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task 其他資料空間的代辦_不應互相看到()
    {
        var (client, personalId) = await CreateUserWithWorkspaceAsync("隔離測試的人");
        var familyId = await client.CreateWorkspaceAsync("我們家", "family");

        await CreateTodoAsync(client, personalId, "general", "個人的事");
        await CreateTodoAsync(client, familyId, "general", "家裡的事");

        Assert.Equal("個人的事", Assert.Single(await ListAsync(client, personalId, "general")).Title);
        Assert.Equal("家裡的事", Assert.Single(await ListAsync(client, familyId, "general")).Title);
    }

    [Fact]
    public async Task 非成員_不可讀取或寫入該資料空間的代辦()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("代辦擁有者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var created = await CreateTodoAsync(owner, familyId, "general", "家庭代辦");

        var outsider = _factory.CreateClient();
        await outsider.LoginAsync(NewSubject(), "外人");

        var read = await outsider.GetAsync(BuildListUrl(familyId, "general"));
        var readOne = await outsider.GetAsync($"/api/todos/{created.Id}");
        var write = await outsider.PostAsJsonAsync("/api/todos", new
        {
            workspaceId = familyId,
            type = "general",
            title = "偷加的",
        });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, readOne.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task 家庭成員_可查看並完成其他成員建立的代辦()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("家庭建立者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var code = await owner.CreateInvitationAsync(familyId);

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "家庭成員");
        (await member.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var created = await CreateTodoAsync(owner, familyId, "shopping", "醬油", quantity: 1);

        // 成員看得到建立者加的項目，也可以直接勾完成
        var seen = Assert.Single(await ListAsync(member, familyId, "shopping"));
        var completed = await SetCompletionAsync(member, created.Id, true);

        Assert.Equal("醬油", seen.Title);
        Assert.Equal("家庭建立者", seen.CreatedByDisplayName);
        Assert.True(completed.IsCompleted);
    }

    [Fact]
    public async Task 空白內容_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("亂填內容的人");

        var response = await client.PostAsJsonAsync("/api/todos", new
        {
            workspaceId,
            type = "general",
            title = "   ",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 不支援的類型_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("亂填類型的人");

        var response = await client.PostAsJsonAsync("/api/todos", new
        {
            workspaceId,
            type = "grocery",
            title = "香蕉",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 數量為零或負數_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("填錯數量的購物者");

        var response = await client.PostAsJsonAsync("/api/todos", new
        {
            workspaceId,
            type = "shopping",
            title = "蘋果",
            quantity = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 未登入_不可存取代辦()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(BuildListUrl(Guid.NewGuid(), "general"));

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

    private static async Task<TodoResponse> CreateTodoAsync(
        HttpClient client,
        Guid workspaceId,
        string type,
        string title,
        int? quantity = null,
        DateTimeOffset? dueAt = null)
    {
        var response = await client.PostAsJsonAsync("/api/todos", new
        {
            workspaceId,
            type,
            title,
            quantity,
            dueAt,
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TodoResponse>())!;
    }

    private static async Task<TodoResponse> SetCompletionAsync(HttpClient client, Guid todoId, bool completed)
    {
        var response = await client.PatchAsJsonAsync($"/api/todos/{todoId}/completion", new { completed });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TodoResponse>())!;
    }

    private static async Task<List<TodoResponse>> ListAsync(
        HttpClient client,
        Guid workspaceId,
        string type,
        bool? completed = null)
    {
        return (await client.GetFromJsonAsync<List<TodoResponse>>(BuildListUrl(workspaceId, type, completed)))!;
    }

    private static string BuildListUrl(Guid workspaceId, string type, bool? completed = null)
    {
        var url = $"/api/todos?workspaceId={workspaceId}&type={type}";

        return completed is null ? url : $"{url}&completed={completed.Value.ToString().ToLowerInvariant()}";
    }

    private static string NewSubject() => $"U{Guid.NewGuid():N}";

    private record TodoResponse(
        Guid Id,
        string Type,
        string Title,
        int? Quantity,
        DateTimeOffset? DueAt,
        DateTimeOffset? CompletedAt,
        bool IsCompleted,
        Guid CreatedByUserId,
        string CreatedByDisplayName);
}
