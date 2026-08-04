using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;
using NestFlow_Backend.Services.External;

namespace NestFlow_Backend.Services;

/// <summary>
/// 取出到期的提醒並以 LINE 推播送出。
/// 取件本身是原子性的（見 <see cref="IReminderRepository.ClaimDueAsync"/>），
/// 因此同一則提醒不會被送出兩次，即使有多個 Worker 同時執行。
/// </summary>
public class ReminderDispatchService : IReminderDispatchService
{
    /// <summary>單次掃描最多處理的筆數，避免一次占用過久。</summary>
    private const int BatchSize = 50;

    /// <summary>失敗重試上限，達到後轉為 Failed 不再嘗試。</summary>
    private const int MaxRetryCount = 3;

    private readonly IReminderRepository _reminderRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILineMessagingClient _messagingClient;
    private readonly ICryptoHelper _cryptoHelper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly LineMessagingOptions _options;
    private readonly ILogger<ReminderDispatchService> _logger;

    public ReminderDispatchService(
        IReminderRepository reminderRepository,
        IUserRepository userRepository,
        ILineMessagingClient messagingClient,
        ICryptoHelper cryptoHelper,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<LineMessagingOptions> options,
        ILogger<ReminderDispatchService> logger)
    {
        _reminderRepository = reminderRepository;
        _userRepository = userRepository;
        _messagingClient = messagingClient;
        _cryptoHelper = cryptoHelper;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReminderDispatchResult> DispatchDueAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var claimed = await _reminderRepository.ClaimDueAsync(now, BatchSize, cancellationToken);

        if (claimed.Count == 0)
        {
            return new ReminderDispatchResult(0, 0, 0, 0);
        }

        var sent = 0;
        var retrying = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var reminder in claimed)
        {
            // 使用者關閉了提醒通知總開關：只略過發送並標記為終態，
            // 不計入失敗或重試，重新開啟總開關後也不會補發這則過期的通知。
            var user = await _userRepository.GetByIdAsync(reminder.UserId, cancellationToken);

            if (user is null || !user.NotificationsEnabled)
            {
                reminder.Status = ReminderStatus.Skipped;
                skipped++;
                continue;
            }

            var externalUserId = await ResolveRecipientAsync(reminder.UserId, cancellationToken);

            var delivered = externalUserId is not null
                && await _messagingClient.PushAsync(
                    externalUserId,
                    BuildMessage(reminder),
                    cancellationToken);

            if (delivered)
            {
                reminder.Status = ReminderStatus.Sent;
                sent++;
                continue;
            }

            reminder.RetryCount++;

            // 收件者無法解析代表尚未綁定 LINE，重試也不會成功，但仍照重試計數收斂
            if (reminder.RetryCount >= MaxRetryCount)
            {
                reminder.Status = ReminderStatus.Failed;
                failed++;

                _logger.LogWarning("提醒 {ReminderId} 已達重試上限，標記為失敗。", reminder.Id);
            }
            else
            {
                // 退回 Pending，下一輪掃描會再取出
                reminder.Status = ReminderStatus.Pending;
                retrying++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReminderDispatchResult(claimed.Count, sent, retrying, failed, skipped);
    }

    /// <summary>
    /// 找出可推播的 LINE 使用者 ID。優先使用 Messaging Channel 的綁定，
    /// 同 Provider 時可退而使用 LINE Login 的身分，開發登入的假身分則排除。
    /// </summary>
    private async Task<string?> ResolveRecipientAsync(Guid userId, CancellationToken cancellationToken)
    {
        var identities = await _userRepository.ListExternalIdentitiesAsync(
            userId,
            IdentityProvider.Line,
            cancellationToken);

        var identity =
            identities.FirstOrDefault(x => x.ChannelId == _options.ChannelId)
            ?? identities.FirstOrDefault(x => x.ChannelId != GlobalConstants.DevChannelId);

        if (identity is null)
        {
            _logger.LogWarning("使用者 {UserId} 沒有可推播的 LINE 身分。", userId);
            return null;
        }

        return _cryptoHelper.Decrypt(identity.ExternalSubject);
    }

    private static string BuildMessage(Reminder reminder)
    {
        return $"⏰ 提醒\n{reminder.Content}";
    }
}
