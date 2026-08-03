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

    private readonly ICalendarEventRepository _eventRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CalendarEventService(
        ICalendarEventRepository eventRepository,
        IWorkspaceService workspaceService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _eventRepository = eventRepository;
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

        return ToDto(calendarEvent, owner?.DisplayName ?? string.Empty);
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

        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            Title = command.Title,
            Description = command.Description,
            StartAt = command.StartAtUtc,
            EndAt = command.EndAtUtc,
            Status = CalendarEventStatus.Active,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        await _eventRepository.AddAsync(calendarEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return ToDto(calendarEvent, user?.DisplayName ?? string.Empty);
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

        calendarEvent.Title = command.Title;
        calendarEvent.Description = command.Description;
        calendarEvent.StartAt = command.StartAtUtc;
        calendarEvent.EndAt = command.EndAtUtc;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var owner = await _userRepository.GetByIdAsync(calendarEvent.UserId, cancellationToken);
        return ToDto(calendarEvent, owner?.DisplayName ?? string.Empty);
    }

    public async Task DeleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var calendarEvent = await GetAccessibleEventAsync(userId, eventId, cancellationToken);

        calendarEvent.Status = CalendarEventStatus.Deleted;

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

        return events
            .Select(x => ToDto(x, x.User?.DisplayName ?? string.Empty))
            .ToList();
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

    private static CalendarEventDtoModel ToDto(CalendarEvent calendarEvent, string displayName) => new()
    {
        Id = calendarEvent.Id,
        Title = calendarEvent.Title,
        Description = calendarEvent.Description,
        StartAt = calendarEvent.StartAt,
        EndAt = calendarEvent.EndAt,
        CreatedByUserId = calendarEvent.UserId,
        CreatedByDisplayName = displayName,
    };
}
