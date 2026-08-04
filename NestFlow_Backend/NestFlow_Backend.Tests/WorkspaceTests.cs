using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NestFlow_Backend.Data;
using static NestFlow_Backend.Tests.TestClientExtensions;

namespace NestFlow_Backend.Tests;

public class WorkspaceTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    public WorkspaceTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 首次登入_應自動建立預設個人資料空間()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "首次登入使用者");

        var workspaces = await client.GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        var personal = Assert.Single(workspaces!);
        Assert.Equal("個人", personal.Name);
        Assert.Equal("personal", personal.Type);
        Assert.Equal("owner", personal.MembershipType);
        Assert.True(personal.IsDefault);
        Assert.Equal(personal.Id, me!.DefaultWorkspaceId);
    }

    [Fact]
    public async Task 使用者可建立家庭資料空間()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "家庭建立者");

        var response = await client.PostAsJsonAsync("/api/workspaces", new { name = "我家", type = "family" });
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("我家", workspace!.Name);
        Assert.Equal("family", workspace.Type);
        Assert.Equal("owner", workspace.MembershipType);
    }

    [Fact]
    public async Task 有效邀請碼_應可加入相同資料空間()
    {
        var (owner, workspaceId, code) = await CreateFamilyWithInvitationAsync();

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "家庭成員");

        var response = await member.PostAsJsonAsync("/api/workspaces/join", new { code });
        var joined = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(workspaceId, joined!.Id);
        Assert.Equal("member", joined.MembershipType);

        // 建立者可以看到兩位成員
        var members = await owner.GetFromJsonAsync<List<MemberResponse>>($"/api/workspaces/{workspaceId}/members");
        Assert.Equal(2, members!.Count);
    }

    [Fact]
    public async Task 已使用過的邀請碼_不可再次使用()
    {
        var (_, _, code) = await CreateFamilyWithInvitationAsync();

        var first = _factory.CreateClient();
        await first.LoginAsync(NewSubject(), "第一位成員");
        (await first.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var second = _factory.CreateClient();
        await second.LoginAsync(NewSubject(), "第二位成員");
        var response = await second.PostAsJsonAsync("/api/workspaces/join", new { code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 過期邀請碼_不可加入()
    {
        var (_, workspaceId, code) = await CreateFamilyWithInvitationAsync();

        // 直接把有效期改到過去，模擬超過 24 小時
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NestFlowDbContext>();
            var invitation = await dbContext.WorkspaceInvitations
                .Where(x => x.WorkspaceId == workspaceId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync();

            invitation.ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
            await dbContext.SaveChangesAsync();
        }

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "遲到的成員");
        var response = await member.PostAsJsonAsync("/api/workspaces/join", new { code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 非成員_無法存取該資料空間()
    {
        var (_, workspaceId, _) = await CreateFamilyWithInvitationAsync();

        var outsider = _factory.CreateClient();
        await outsider.LoginAsync(NewSubject(), "外人");

        var members = await outsider.GetAsync($"/api/workspaces/{workspaceId}/members");
        var rename = await outsider.PutAsJsonAsync($"/api/workspaces/{workspaceId}", new { name = "被改名" });

        // 為避免洩漏資料空間是否存在，非成員一律得到 404
        Assert.Equal(HttpStatusCode.NotFound, members.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
    }

    [Fact]
    public async Task 成員被移除後_立即無法存取該資料空間()
    {
        var (owner, workspaceId, code) = await CreateFamilyWithInvitationAsync();

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "將被移除的成員");
        (await member.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var memberId = (await member.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!.Id;

        // 移除前可以存取
        Assert.Equal(
            HttpStatusCode.OK,
            (await member.GetAsync($"/api/workspaces/{workspaceId}/members")).StatusCode);

        var removed = await owner.DeleteAsync($"/api/workspaces/{workspaceId}/members/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        // 移除後立即失去存取權
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await member.GetAsync($"/api/workspaces/{workspaceId}/members")).StatusCode);

        var workspaces = await member.GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");
        Assert.DoesNotContain(workspaces!, x => x.Id == workspaceId);
    }

    [Fact]
    public async Task 成員可主動離開資料空間()
    {
        var (_, workspaceId, code) = await CreateFamilyWithInvitationAsync();

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "想離開的成員");
        (await member.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var memberId = (await member.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!.Id;

        var left = await member.DeleteAsync($"/api/workspaces/{workspaceId}/members/{memberId}");

        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await member.GetAsync($"/api/workspaces/{workspaceId}/members")).StatusCode);
    }

    [Fact]
    public async Task 成員不可移除其他成員()
    {
        var (_, workspaceId, code) = await CreateFamilyWithInvitationAsync();

        var memberA = _factory.CreateClient();
        await memberA.LoginAsync(NewSubject(), "成員A");
        (await memberA.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var members = await memberA.GetFromJsonAsync<List<MemberResponse>>($"/api/workspaces/{workspaceId}/members");
        var ownerId = members!.Single(x => x.MembershipType == "owner").UserId;

        var response = await memberA.DeleteAsync($"/api/workspaces/{workspaceId}/members/{ownerId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task 個人資料空間_不可產生邀請碼()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "個人使用者");

        var workspaces = await client.GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");
        var personalId = workspaces!.Single().Id;

        var response = await client.PostAsync($"/api/workspaces/{personalId}/invitations", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 未登入_不可存取資料空間()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 登出後_Session立即失效()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "要登出的使用者");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/workspaces")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/workspaces")).StatusCode);
    }

    [Fact]
    public async Task 可設定預設資料空間()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "切換預設的使用者");

        var familyId = await client.CreateWorkspaceAsync("我家", "family");

        var response = await client.PutAsJsonAsync("/api/workspaces/default", new { workspaceId = familyId });
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(familyId, me!.DefaultWorkspaceId);
    }

    [Fact]
    public async Task 提醒通知總開關_預設開啟且可切換()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "切換通知開關的使用者");

        var before = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.True(before!.NotificationsEnabled);

        var response = await client.PutAsJsonAsync("/api/auth/me/notifications", new { enabled = false });
        var after = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(after!.NotificationsEnabled);
    }

    [Fact]
    public async Task 刪除預設資料空間後_應自動改指向其他可用空間()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "刪除預設空間的使用者");

        // 首次登入自動建立「個人」並設為預設，這裡再建立第二個當作刪除後的備援
        var familyId = await client.CreateWorkspaceAsync("我家", "family");
        var meBefore = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        var personalId = meBefore!.DefaultWorkspaceId!.Value;

        var deleted = await client.DeleteAsync($"/api/workspaces/{personalId}");
        var meAfter = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(familyId, meAfter!.DefaultWorkspaceId);
    }

    [Fact]
    public async Task 刪除唯一的資料空間後_預設應變為null不留下懸空Id()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "只有一個空間的使用者");

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        var personalId = me!.DefaultWorkspaceId!.Value;

        var deleted = await client.DeleteAsync($"/api/workspaces/{personalId}");
        var meAfter = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        var workspaces = await client.GetFromJsonAsync<List<WorkspaceResponse>>("/api/workspaces");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Null(meAfter!.DefaultWorkspaceId);
        Assert.Empty(workspaces!);
    }

    [Fact]
    public async Task 移除成員後_若原為其預設空間應自動改指向其他可用空間()
    {
        var (owner, workspaceId, code) = await CreateFamilyWithInvitationAsync();

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "被移除且設為預設的成員");
        (await member.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var meBefore = await member.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        var memberPersonalId = meBefore!.DefaultWorkspaceId!.Value;

        // 把預設改成剛加入的家庭空間，之後被移除時應偵測到並改回個人空間
        await member.PutAsJsonAsync("/api/workspaces/default", new { workspaceId });

        var removed = await owner.DeleteAsync($"/api/workspaces/{workspaceId}/members/{meBefore.Id}");
        var meAfter = await member.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(memberPersonalId, meAfter!.DefaultWorkspaceId);
    }

    [Fact]
    public async Task 建立資料空間時_只接受personal或family()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(NewSubject(), "亂填類型的使用者");

        var response = await client.PostAsJsonAsync("/api/workspaces", new { name = "公司", type = "company" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(HttpClient Owner, Guid WorkspaceId, string Code)> CreateFamilyWithInvitationAsync()
    {
        var owner = _factory.CreateClient();
        await owner.LoginAsync(NewSubject(), "家庭建立者");

        var workspaceId = await owner.CreateWorkspaceAsync("我家", "family");
        var code = await owner.CreateInvitationAsync(workspaceId);

        return (owner, workspaceId, code);
    }

    /// <summary>每個測試使用不同的外部識別碼，代表不同的 LINE 使用者。</summary>
    private static string NewSubject() => $"U{Guid.NewGuid():N}";
}
