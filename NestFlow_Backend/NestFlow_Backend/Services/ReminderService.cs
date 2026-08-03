using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;

namespace NestFlow_Backend.Services;

public class ReminderService : IReminderService
{
    /// <summary>單次查詢的最大筆數，避免一次拉回過量資料。</summary>
    private const int MaxLimit = 500;

    /// <summary>提醒最遠只能排到一年後，避免誤填年份。</summary>
    private static readonly TimeSpan MaxLeadTime = TimeSpan.FromDays(365);

    private readonly IReminderRepository _reminderRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ReminderService(
        IReminderRepository reminderRepository,
        IWorkspaceService workspaceService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _reminderRepository = reminderRepository;
        _workspaceService = workspaceService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<ReminderDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveReminderCommand command,
        CancellationToken cancellationToken)
    {
        // 前端傳入的 workspaceId 一律重新驗證 Membership
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);

        var now = _timeProvider.GetUtcNow();

        if (command.TriggerAtUtc <= now)
        {
            throw AppException.BadRequest("提醒時間必須晚於現在。");
        }

        if (command.TriggerAtUtc - now > MaxLeadTime)
        {
            throw AppException.BadRequest("提醒時間不可超過一年後。");
        }

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            Content = command.Content,
            TriggerAt = command.TriggerAtUtc,
            NotificationProvider = IdentityProvider.Line,
            Status = ReminderStatus.Pending,
            RetryCount = 0,
            CreatedAt = now,
        };

        await _reminderRepository.AddAsync(reminder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return ToDto(reminder, user?.DisplayName ?? string.Empty);
    }

    public async Task CancelAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken)
    {
        var reminder = await _reminderRepository.GetAsync(reminderId, cancellationToken)
            ?? throw AppException.NotFound();

        // 家庭空間內任何成員都可取消共同提醒，符合計畫第 7.4 節
        await _workspaceService.EnsureMemberAsync(userId, reminder.WorkspaceId, cancellationToken);

        if (reminder.Status is ReminderStatus.Sent or ReminderStatus.Sending)
        {
            throw AppException.BadRequest("提醒已發送，無法取消。");
        }

        reminder.Status = ReminderStatus.Cancelled;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ReminderDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);

        if (toUtc <= fromUtc)
        {
            throw AppException.BadRequest("結束時間必須晚於開始時間。");
        }

        if (toUtc - fromUtc > TimeSpan.FromDays(400))
        {
            throw AppException.BadRequest("查詢區間不可超過 400 天。");
        }

        var reminders = await _reminderRepository.ListAsync(
            workspaceId,
            fromUtc,
            toUtc,
            Math.Min(limit ?? MaxLimit, MaxLimit),
            cancellationToken);

        return reminders
            .Select(x => ToDto(x, x.User?.DisplayName ?? string.Empty))
            .ToList();
    }

    private static ReminderDtoModel ToDto(Reminder reminder, string displayName) => new()
    {
        Id = reminder.Id,
        Content = reminder.Content,
        TriggerAt = reminder.TriggerAt,
        Status = reminder.Status,
        RetryCount = reminder.RetryCount,
        CreatedByUserId = reminder.UserId,
        CreatedByDisplayName = displayName,
    };
}
