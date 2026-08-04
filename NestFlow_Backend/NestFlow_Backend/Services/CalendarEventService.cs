using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;

namespace NestFlow_Backend.Services;

public class CalendarEventService : ICalendarEventService
{
    /// <summary>單次查詢的最大筆數，避免一次拉回過量資料。</summary>
    private const int MaxLimit = 500;

    /// <summary>單筆行程的最長時間，避免誤填年份造成整個月曆被一筆資料佔滿。</summary>
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(30);

    /// <summary>週期行程最多產生的場次數，每週一次約等於 2 年，避免無限生成。</summary>
    private const int MaxRecurrenceOccurrences = 104;

    private readonly ICalendarEventRepository _eventRepository;
    private readonly IReminderRepository _reminderRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CalendarEventService(
        ICalendarEventRepository eventRepository,
        IReminderRepository reminderRepository,
        IWorkspaceService workspaceService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _eventRepository = eventRepository;
        _reminderRepository = reminderRepository;
        _workspaceService = workspaceService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<CalendarEventDtoModel> GetAsync(
        Guid userId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await GetAccessibleEventAsync(userId, eventId, cancellationToken);
        var owner = await _userRepository.GetByIdAsync(calendarEvent.UserId, cancellationToken);
        var reminder = await _reminderRepository.GetByCalendarEventIdAsync(eventId, cancellationToken);

        return ToDto(
            calendarEvent,
            owner?.DisplayName ?? string.Empty,
            reminder is not null,
            LeadMinutesOf(calendarEvent, reminder));
    }

    public async Task<CalendarEventDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveCalendarEventCommand command,
        CancellationToken cancellationToken)
    {
        // 前端傳入的 workspaceId 一律重新驗證 Membership
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);
        EnsureValidPeriod(command);

        var leadMinutes = command.WantsReminder
            ? CalendarReminderOptions.Normalize(command.ReminderMinutesBeforeStart)
            : (int?)null;

        var now = _timeProvider.GetUtcNow();
        var occurrences = BuildOccurrences(command);
        var recurrenceGroupId = occurrences.Count > 1 ? Guid.NewGuid() : (Guid?)null;

        CalendarEvent? first = null;

        foreach (var (start, end) in occurrences)
        {
            var calendarEvent = new CalendarEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WorkspaceId = workspaceId,
                RecurrenceGroupId = recurrenceGroupId,
                Title = command.Title,
                Description = command.Description,
                StartAt = start,
                EndAt = end,
                Status = CalendarEventStatus.Active,
                CreatedAt = now,
            };

            await _eventRepository.AddAsync(calendarEvent, cancellationToken);
            await SyncReminderAsync(calendarEvent, leadMinutes, now, cancellationToken);

            first ??= calendarEvent;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return ToDto(first!, user?.DisplayName ?? string.Empty, leadMinutes is not null, leadMinutes);
    }

    public async Task<CalendarEventDtoModel> UpdateAsync(
        Guid userId,
        Guid eventId,
        Guid workspaceId,
        SaveCalendarEventCommand command,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await GetAccessibleEventAsync(userId, eventId, cancellationToken);

        // 第一版不支援把行程搬到其他資料空間
        if (calendarEvent.WorkspaceId != workspaceId)
        {
            throw AppException.BadRequest("不能變更行程所屬的資料空間。");
        }

        EnsureValidPeriod(command);

        var leadMinutes = command.WantsReminder
            ? CalendarReminderOptions.Normalize(command.ReminderMinutesBeforeStart)
            : (int?)null;

        var now = _timeProvider.GetUtcNow();

        if (calendarEvent.RecurrenceGroupId is null)
        {
            calendarEvent.Title = command.Title;
            calendarEvent.Description = command.Description;
            calendarEvent.StartAt = command.StartAtUtc;
            calendarEvent.EndAt = command.EndAtUtc;

            await SyncReminderAsync(calendarEvent, leadMinutes, now, cancellationToken);
        }
        else
        {
            await UpdateSeriesAsync(calendarEvent, command, leadMinutes, now, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var owner = await _userRepository.GetByIdAsync(calendarEvent.UserId, cancellationToken);
        return ToDto(calendarEvent, owner?.DisplayName ?? string.Empty, leadMinutes is not null, leadMinutes);
    }

    public async Task DeleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var calendarEvent = await GetAccessibleEventAsync(userId, eventId, cancellationToken);

        calendarEvent.Status = CalendarEventStatus.Deleted;
        await CancelReminderAsync(eventId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSeriesAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var calendarEvent = await GetAccessibleEventAsync(userId, eventId, cancellationToken);

        if (calendarEvent.RecurrenceGroupId is null)
        {
            // 非週期行程沒有系列可言，等同刪除單筆
            await DeleteAsync(userId, eventId, cancellationToken);
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var siblings = await _eventRepository.ListActiveBySeriesAsync(
            calendarEvent.RecurrenceGroupId.Value,
            cancellationToken);

        // 只刪除尚未發生的場次，已過去的保留作為歷史紀錄
        foreach (var sibling in siblings.Where(x => x.StartAt >= now))
        {
            sibling.Status = CalendarEventStatus.Deleted;
            await CancelReminderAsync(sibling.Id, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<CalendarEventDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);
        EnsureValidRange(fromUtc, toUtc);

        var events = await _eventRepository.ListAsync(
            workspaceId,
            fromUtc,
            toUtc,
            Math.Min(limit ?? MaxLimit, MaxLimit),
            cancellationToken);

        var reminders = await _reminderRepository.ListByCalendarEventIdsAsync(
            events.Select(x => x.Id).ToList(),
            cancellationToken);

        var reminderByEventId = reminders
            .Where(x => x.CalendarEventId is not null)
            .ToDictionary(x => x.CalendarEventId!.Value);

        return events
            .Select(x =>
            {
                reminderByEventId.TryGetValue(x.Id, out var reminder);

                return ToDto(
                    x,
                    x.User?.DisplayName ?? string.Empty,
                    reminder is not null,
                    LeadMinutesOf(x, reminder));
            })
            .ToList();
    }

    /// <summary>
    /// 整系列更新：只影響「尚未發生」的場次，已過去的場次保留原樣不受影響。
    /// 日期本身不可變更（會破壞每週固定星期幾的規律），只能調整時間、長度、標題、說明與提醒設定，
    /// 要跳過某一場請改用「僅此次取消」。
    /// </summary>
    private async Task UpdateSeriesAsync(
        CalendarEvent editedEvent,
        SaveCalendarEventCommand command,
        int? leadMinutes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (command.StartAtUtc.UtcDateTime.Date != editedEvent.StartAt.UtcDateTime.Date)
        {
            throw AppException.BadRequest("週期行程不能修改日期，如需跳過請使用「僅此次取消」。");
        }

        var newTimeOfDay = command.StartAtUtc.UtcDateTime.TimeOfDay;
        var newDuration = command.EndAtUtc - command.StartAtUtc;

        var siblings = await _eventRepository.ListActiveBySeriesAsync(
            editedEvent.RecurrenceGroupId!.Value,
            cancellationToken);

        foreach (var sibling in siblings.Where(x => x.StartAt >= now))
        {
            sibling.Title = command.Title;
            sibling.Description = command.Description;

            var siblingDate = sibling.StartAt.UtcDateTime.Date;
            sibling.StartAt = new DateTimeOffset(siblingDate, TimeSpan.Zero).Add(newTimeOfDay);
            sibling.EndAt = sibling.StartAt + newDuration;

            await SyncReminderAsync(sibling, leadMinutes, now, cancellationToken);
        }
    }

    /// <summary>
    /// 依目前的提醒設定，建立、更新或取消該行程對應的提醒。
    /// 已發送或發送中的提醒無法收回，維持原樣不處理。
    /// </summary>
    private async Task SyncReminderAsync(
        CalendarEvent calendarEvent,
        int? leadMinutes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await _reminderRepository.GetByCalendarEventIdAsync(calendarEvent.Id, cancellationToken);

        if (leadMinutes is null)
        {
            if (existing is { Status: ReminderStatus.Pending })
            {
                existing.Status = ReminderStatus.Cancelled;
            }

            return;
        }

        var triggerAt = calendarEvent.StartAt.AddMinutes(-leadMinutes.Value);

        if (existing is not null)
        {
            if (existing.Status != ReminderStatus.Pending)
            {
                return;
            }

            if (triggerAt <= now)
            {
                // 新的觸發時間已經過去，等同取消
                existing.Status = ReminderStatus.Cancelled;
                return;
            }

            existing.Content = calendarEvent.Title;
            existing.TriggerAt = triggerAt;
            return;
        }

        // 觸發時間已過去的提醒沒有意義，不建立
        if (triggerAt <= now)
        {
            return;
        }

        await _reminderRepository.AddAsync(
            new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = calendarEvent.UserId,
                WorkspaceId = calendarEvent.WorkspaceId,
                CalendarEventId = calendarEvent.Id,
                Content = calendarEvent.Title,
                TriggerAt = triggerAt,
                NotificationProvider = IdentityProvider.Line,
                Status = ReminderStatus.Pending,
                RetryCount = 0,
                CreatedAt = now,
            },
            cancellationToken);
    }

    private async Task CancelReminderAsync(Guid calendarEventId, CancellationToken cancellationToken)
    {
        var reminder = await _reminderRepository.GetByCalendarEventIdAsync(calendarEventId, cancellationToken);

        if (reminder is { Status: ReminderStatus.Pending })
        {
            reminder.Status = ReminderStatus.Cancelled;
        }
    }

    /// <summary>
    /// 取得行程並確認呼叫者是該 Workspace 的有效成員。
    /// 家庭空間內任何成員都可修改與刪除共同行程，符合計畫第 7.4 節。
    /// </summary>
    private async Task<CalendarEvent> GetAccessibleEventAsync(
        Guid userId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await _eventRepository.GetActiveAsync(eventId, cancellationToken)
            ?? throw AppException.NotFound();

        await _workspaceService.EnsureMemberAsync(userId, calendarEvent.WorkspaceId, cancellationToken);

        return calendarEvent;
    }

    /// <summary>
    /// 依結束方式產生所有場次的開始／結束時間，固定每 7 天一次（同星期幾）。
    /// 未設定重複規則時只回傳原本這一場。
    /// </summary>
    private static List<(DateTimeOffset Start, DateTimeOffset End)> BuildOccurrences(SaveCalendarEventCommand command)
    {
        var first = (command.StartAtUtc, command.EndAtUtc);

        if (command.RecurrenceEndType is null)
        {
            return [first];
        }

        var duration = command.EndAtUtc - command.StartAtUtc;
        var occurrences = new List<(DateTimeOffset, DateTimeOffset)> { first };

        var maxCount = command.RecurrenceEndType == CalendarRecurrenceEndType.Count
            ? ValidateRecurrenceCount(command.RecurrenceCount)
            : MaxRecurrenceOccurrences;

        DateTimeOffset? untilUtc = command.RecurrenceEndType == CalendarRecurrenceEndType.UntilDate
            ? ValidateRecurrenceUntil(command.RecurrenceUntilUtc, command.StartAtUtc)
            : null;

        var cursorStart = command.StartAtUtc;

        while (occurrences.Count < maxCount)
        {
            cursorStart = cursorStart.AddDays(7);

            if (untilUtc is not null && cursorStart > untilUtc.Value)
            {
                break;
            }

            occurrences.Add((cursorStart, cursorStart + duration));
        }

        return occurrences;
    }

    private static int ValidateRecurrenceCount(int? count)
    {
        if (count is null or < 1)
        {
            throw AppException.BadRequest("重複次數必須至少 1 次。");
        }

        return Math.Min(count.Value, MaxRecurrenceOccurrences);
    }

    private static DateTimeOffset ValidateRecurrenceUntil(DateTimeOffset? until, DateTimeOffset startUtc)
    {
        if (until is null || until.Value <= startUtc)
        {
            throw AppException.BadRequest("重複結束日期必須晚於開始時間。");
        }

        if (until.Value - startUtc > TimeSpan.FromDays(730))
        {
            throw AppException.BadRequest("重複結束日期不可超過 2 年。");
        }

        return until.Value;
    }

    private static void EnsureValidPeriod(SaveCalendarEventCommand command)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            throw AppException.BadRequest("結束時間必須晚於開始時間。");
        }

        if (command.EndAtUtc - command.StartAtUtc > MaxDuration)
        {
            throw AppException.BadRequest("單筆行程不可超過 30 天。");
        }
    }

    private static void EnsureValidRange(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (toUtc <= fromUtc)
        {
            throw AppException.BadRequest("結束時間必須晚於開始時間。");
        }

        if (toUtc - fromUtc > TimeSpan.FromDays(400))
        {
            throw AppException.BadRequest("查詢區間不可超過 400 天。");
        }
    }

    private static int? LeadMinutesOf(CalendarEvent calendarEvent, Reminder? reminder)
    {
        return reminder is null
            ? null
            : (int)Math.Round((calendarEvent.StartAt - reminder.TriggerAt).TotalMinutes);
    }

    private static CalendarEventDtoModel ToDto(
        CalendarEvent calendarEvent,
        string displayName,
        bool hasReminder,
        int? reminderMinutesBeforeStart) => new()
    {
        Id = calendarEvent.Id,
        Title = calendarEvent.Title,
        Description = calendarEvent.Description,
        StartAt = calendarEvent.StartAt,
        EndAt = calendarEvent.EndAt,
        HasReminder = hasReminder,
        ReminderMinutesBeforeStart = reminderMinutesBeforeStart,
        IsRecurring = calendarEvent.RecurrenceGroupId is not null,
        CreatedByUserId = calendarEvent.UserId,
        CreatedByDisplayName = displayName,
    };
}
