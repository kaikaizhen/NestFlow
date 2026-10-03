using System.Net;
using System.Net.Http.Json;
using static NestFlow_Backend.Tests.TestClientExtensions;

namespace NestFlow_Backend.Tests;

/// <summary>Module 5：PWA 行程。</summary>
public class CalendarEventTests : IClassFixture<NestFlowApiFactory>
{
    private readonly NestFlowApiFactory _factory;

    /// <summary>測試用的月份區間，模擬前端以 Asia/Taipei 換算後的 UTC 起訖。</summary>
    private static readonly DateTimeOffset MonthFrom = new(2035, 7, 31, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MonthTo = new(2035, 8, 31, 16, 0, 0, TimeSpan.Zero);

    /// <summary>台灣時間 2026/8/10 12:00 ~ 13:00。</summary>
    private static readonly DateTimeOffset Start = new(2035, 8, 10, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2035, 8, 10, 5, 0, 0, TimeSpan.Zero);

    public CalendarEventTests(NestFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task 新增行程_應可在列表中看到()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("建立行程的人");

        var created = await CreateEventAsync(client, workspaceId, "專案進度會議", "會議室 A");

        var events = await ListAsync(client, workspaceId);
        var single = Assert.Single(events);

        Assert.Equal(created.Id, single.Id);
        Assert.Equal("專案進度會議", single.Title);
        Assert.Equal("會議室 A", single.Description);
        Assert.Equal(Start, single.StartAt);
        Assert.Equal(End, single.EndAt);
        Assert.Equal("建立行程的人", single.CreatedByDisplayName);
    }

    [Fact]
    public async Task 修改行程_應更新內容與時間()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("改行程的人");
        var created = await CreateEventAsync(client, workspaceId, "原標題", null);

        var response = await client.PutAsJsonAsync($"/api/calendar-events/{created.Id}", new
        {
            workspaceId,
            title = "新標題",
            description = "改到線上會議",
            startAt = Start.AddHours(1),
            endAt = End.AddHours(2),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = Assert.Single(await ListAsync(client, workspaceId));

        Assert.Equal("新標題", updated.Title);
        Assert.Equal("改到線上會議", updated.Description);
        Assert.Equal(Start.AddHours(1), updated.StartAt);
        Assert.Equal(End.AddHours(2), updated.EndAt);
    }

    [Fact]
    public async Task 軟刪除後_不應出現在列表()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("刪行程的人");
        var created = await CreateEventAsync(client, workspaceId, "要刪掉的行程", null);

        var deleted = await client.DeleteAsync($"/api/calendar-events/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await ListAsync(client, workspaceId));

        // 已刪除的行程也不能再讀取或修改
        var read = await client.GetAsync($"/api/calendar-events/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task 區間外的行程_不應被取得()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("跨月的人");

        // 台灣時間 9/1 10:00，屬於 9 月
        await CreateEventAsync(
            client,
            workspaceId,
            "下個月的行程",
            null,
            new DateTimeOffset(2035, 9, 1, 2, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2035, 9, 1, 3, 0, 0, TimeSpan.Zero));

        await CreateEventAsync(client, workspaceId, "這個月的行程", null);

        var events = await ListAsync(client, workspaceId);

        Assert.Equal("這個月的行程", Assert.Single(events).Title);
    }

    [Fact]
    public async Task 跨月的行程_兩個月都應取得()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("跨月行程的人");

        // 台灣時間 8/31 20:00 ~ 9/1 10:00
        await CreateEventAsync(
            client,
            workspaceId,
            "跨月出差",
            null,
            new DateTimeOffset(2035, 8, 31, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2035, 9, 1, 2, 0, 0, TimeSpan.Zero));

        var august = await ListAsync(client, workspaceId);
        var september = await ListAsync(
            client,
            workspaceId,
            MonthTo,
            new DateTimeOffset(2035, 9, 30, 16, 0, 0, TimeSpan.Zero));

        Assert.Equal("跨月出差", Assert.Single(august).Title);
        Assert.Equal("跨月出差", Assert.Single(september).Title);
    }

    [Fact]
    public async Task 列表_應依開始時間由早到晚()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("排序測試者");

        await CreateEventAsync(client, workspaceId, "下午", null, Start.AddHours(5), End.AddHours(5));
        await CreateEventAsync(client, workspaceId, "早上", null, Start, End);
        await CreateEventAsync(client, workspaceId, "中午", null, Start.AddHours(2), End.AddHours(2));

        var titles = (await ListAsync(client, workspaceId)).Select(x => x.Title).ToList();

        Assert.Equal(["早上", "中午", "下午"], titles);
    }

    [Fact]
    public async Task 其他資料空間的行程_不應互相看到()
    {
        var (client, personalId) = await CreateUserWithWorkspaceAsync("隔離測試者");
        var familyId = await client.CreateWorkspaceAsync("我們家", "family");

        await CreateEventAsync(client, personalId, "個人看牙醫", null);
        await CreateEventAsync(client, familyId, "家庭聚餐", null);

        Assert.Equal("個人看牙醫", Assert.Single(await ListAsync(client, personalId)).Title);
        Assert.Equal("家庭聚餐", Assert.Single(await ListAsync(client, familyId)).Title);
    }

    [Fact]
    public async Task 非成員_不可讀取或寫入該資料空間的行程()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("空間擁有者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var created = await CreateEventAsync(owner, familyId, "家庭行程", null);

        var outsider = _factory.CreateClient();
        await outsider.LoginAsync(NewSubject(), "外人");

        var read = await outsider.GetAsync(BuildListUrl(familyId, MonthFrom, MonthTo));
        var readOne = await outsider.GetAsync($"/api/calendar-events/{created.Id}");
        var write = await outsider.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId = familyId,
            title = "偷加的行程",
            startAt = Start,
            endAt = End,
        });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, readOne.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task 家庭成員_可修改其他成員建立的行程()
    {
        var (owner, _) = await CreateUserWithWorkspaceAsync("家庭建立者");
        var familyId = await owner.CreateWorkspaceAsync("我們家", "family");
        var code = await owner.CreateInvitationAsync(familyId);

        var member = _factory.CreateClient();
        await member.LoginAsync(NewSubject(), "家庭成員");
        (await member.PostAsJsonAsync("/api/workspaces/join", new { code })).EnsureSuccessStatusCode();

        var created = await CreateEventAsync(owner, familyId, "建立者排的行程", null);

        var response = await member.PutAsJsonAsync($"/api/calendar-events/{created.Id}", new
        {
            workspaceId = familyId,
            title = "成員改的行程",
            startAt = Start,
            endAt = End,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 結束時間不晚於開始時間_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("時間填反的人");

        var response = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "時間填反",
            startAt = End,
            endAt = Start,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 沒有標題_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("沒填標題的人");

        var response = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "",
            startAt = Start,
            endAt = End,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 未登入_不可存取行程()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(BuildListUrl(Guid.NewGuid(), MonthFrom, MonthTo));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------
    // Module 7：行程提醒整合
    // -----------------------------------------------------------
    [Fact]
    public async Task 新增行程並開啟提醒_應建立對應的提醒()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("開提醒的人");

        var created = await CreateEventWithReminderAsync(client, workspaceId, "開會", 30);

        Assert.True(created.HasReminder);
        Assert.Equal(30, created.ReminderMinutesBeforeStart);

        var reminder = Assert.Single(await ListRemindersAsync(client, workspaceId));
        Assert.Equal("開會", reminder.Content);
        Assert.Equal(Start.AddMinutes(-30), reminder.TriggerAt);
    }

    [Fact]
    public async Task 修改行程關閉提醒_應取消原有提醒()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("關提醒的人");
        var created = await CreateEventWithReminderAsync(client, workspaceId, "開會", 30);

        var response = await client.PutAsJsonAsync($"/api/calendar-events/{created.Id}", new
        {
            workspaceId,
            title = "開會",
            startAt = Start,
            endAt = End,
            wantsReminder = false,
        });

        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();

        Assert.False(updated!.HasReminder);
        Assert.Empty(await ListRemindersAsync(client, workspaceId));
    }

    [Fact]
    public async Task 刪除行程_應一併取消其提醒()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("刪行程連提醒的人");
        var created = await CreateEventWithReminderAsync(client, workspaceId, "開會", 30);

        (await client.DeleteAsync($"/api/calendar-events/{created.Id}")).EnsureSuccessStatusCode();

        Assert.Empty(await ListRemindersAsync(client, workspaceId));
    }

    [Fact]
    public async Task 不支援的提前通知分鐘數_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("亂填提前時間的人");

        var response = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "開會",
            startAt = Start,
            endAt = End,
            wantsReminder = true,
            reminderMinutesBeforeStart = 7,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------
    // Module 7：週期行程
    // -----------------------------------------------------------
    [Fact]
    public async Task 建立週期行程_重複次數應產生對應場數()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("週期行程的人");

        var response = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "每週會議",
            startAt = Start,
            endAt = End,
            repeat = true,
            repeatEndType = "count",
            repeatCount = 4,
        });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();

        Assert.True(created!.IsRecurring);

        // 查詢橫跨 4 週的區間，應看到全部 4 場
        var events = await ListAsync(client, workspaceId, MonthFrom, MonthTo.AddDays(28));

        Assert.Equal(4, events.Count(x => x.Title == "每週會議"));
    }

    [Fact]
    public async Task 週期行程重複結束日超過兩年_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("結束日填太遠的人");

        var response = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "太久的重複",
            startAt = Start,
            endAt = End,
            repeat = true,
            repeatEndType = "until",
            repeatUntil = Start.AddDays(800),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 修改週期行程_應同步更新未來場次但不影響過去場次()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("改週期行程的人");

        // 第一場落在 10 天前，往後每 7 天一場：第 1 場已過去，其餘 3 場尚未發生。
        // 刻意與「現在」保持數天的安全距離，避免測試執行時的些微延遲造成邊界誤判。
        var now = DateTimeOffset.UtcNow;
        var pastStart = now.AddDays(-10);
        var pastEnd = pastStart.AddHours(1);

        var createResponse = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "原標題",
            startAt = pastStart,
            endAt = pastEnd,
            repeat = true,
            repeatEndType = "count",
            repeatCount = 4,
        });

        createResponse.EnsureSuccessStatusCode();

        var events = (await ListAsync(client, workspaceId, now.AddDays(-30), now.AddDays(30)))
            .OrderBy(x => x.StartAt)
            .ToList();

        var futureOccurrence = events.First(x => x.StartAt >= now);

        var response = await client.PutAsJsonAsync($"/api/calendar-events/{futureOccurrence.Id}", new
        {
            workspaceId,
            title = "新標題",
            startAt = futureOccurrence.StartAt.AddHours(1),
            endAt = futureOccurrence.EndAt.AddHours(1),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = (await ListAsync(client, workspaceId, now.AddDays(-30), now.AddDays(30)))
            .OrderBy(x => x.StartAt)
            .ToList();

        // 過去那場標題不變
        Assert.Contains(refreshed, x => x.Title == "原標題" && x.StartAt < now);

        // 未來場次全部改為新標題與新時間
        Assert.All(refreshed.Where(x => x.StartAt >= now), x => Assert.Equal("新標題", x.Title));
    }

    [Fact]
    public async Task 修改週期行程時變更日期_應被拒絕()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("想改日期的人");

        var createResponse = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "每週會議",
            startAt = Start,
            endAt = End,
            repeat = true,
            repeatEndType = "count",
            repeatCount = 3,
        });

        var created = await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        var response = await client.PutAsJsonAsync($"/api/calendar-events/{created!.Id}", new
        {
            workspaceId,
            title = "每週會議",
            startAt = Start.AddDays(1),
            endAt = End.AddDays(1),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 僅刪除此次_不應影響其他場次()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("跳過某一場的人");

        var createResponse = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "每週會議",
            startAt = Start,
            endAt = End,
            repeat = true,
            repeatEndType = "count",
            repeatCount = 3,
        });

        var created = await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        (await client.DeleteAsync($"/api/calendar-events/{created!.Id}")).EnsureSuccessStatusCode();

        var events = await ListAsync(client, workspaceId, MonthFrom, MonthTo.AddDays(28));

        // 只少了被跳過的那一場，其餘兩場仍在
        Assert.Equal(2, events.Count(x => x.Title == "每週會議"));
    }

    [Fact]
    public async Task 刪除整個系列_應移除尚未發生的所有場次()
    {
        var (client, workspaceId) = await CreateUserWithWorkspaceAsync("刪整系列的人");

        var createResponse = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title = "每週會議",
            startAt = Start,
            endAt = End,
            repeat = true,
            repeatEndType = "count",
            repeatCount = 3,
        });

        var created = await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        var response = await client.DeleteAsync($"/api/calendar-events/{created!.Id}/series");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var events = await ListAsync(client, workspaceId, MonthFrom, MonthTo.AddDays(28));

        Assert.DoesNotContain(events, x => x.Title == "每週會議");
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

    private static async Task<EventResponse> CreateEventAsync(
        HttpClient client,
        Guid workspaceId,
        string title,
        string? description,
        DateTimeOffset? startAt = null,
        DateTimeOffset? endAt = null)
    {
        var response = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title,
            description,
            startAt = startAt ?? Start,
            endAt = endAt ?? End,
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    private static async Task<EventResponse> CreateEventWithReminderAsync(
        HttpClient client,
        Guid workspaceId,
        string title,
        int reminderMinutesBeforeStart)
    {
        var response = await client.PostAsJsonAsync("/api/calendar-events", new
        {
            workspaceId,
            title,
            startAt = Start,
            endAt = End,
            wantsReminder = true,
            reminderMinutesBeforeStart,
        });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    private static async Task<List<ReminderResponse>> ListRemindersAsync(HttpClient client, Guid workspaceId)
    {
        var from = Encode(Start.AddDays(-1));
        var to = Encode(Start.AddDays(1));

        return (await client.GetFromJsonAsync<List<ReminderResponse>>(
            $"/api/reminders?workspaceId={workspaceId}&from={from}&to={to}"))!;
    }

    private static async Task<List<EventResponse>> ListAsync(
        HttpClient client,
        Guid workspaceId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        return (await client.GetFromJsonAsync<List<EventResponse>>(
            BuildListUrl(workspaceId, from ?? MonthFrom, to ?? MonthTo)))!;
    }

    private static string BuildListUrl(Guid workspaceId, DateTimeOffset from, DateTimeOffset to)
    {
        return $"/api/calendar-events?workspaceId={workspaceId}&from={Encode(from)}&to={Encode(to)}";
    }

    private static string Encode(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));

    private static string NewSubject() => $"U{Guid.NewGuid():N}";

    private record EventResponse(
        Guid Id,
        string Title,
        string? Description,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        bool HasReminder,
        int? ReminderMinutesBeforeStart,
        bool IsRecurring,
        Guid CreatedByUserId,
        string CreatedByDisplayName);

    private record ReminderResponse(
        Guid Id,
        string Content,
        DateTimeOffset TriggerAt,
        string Status,
        int RetryCount,
        Guid CreatedByUserId,
        string CreatedByDisplayName);
}
