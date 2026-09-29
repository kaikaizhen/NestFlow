using System.Text.Json;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;
using NestFlow_Backend.Services.External;

namespace NestFlow_Backend.Services;

/// <summary>
/// LINE 固定格式記帳流程。
/// 訊息只會產生待確認動作，使用者回覆「確認」後才真正寫入資料。
/// 固定格式解析不出結果時，改呼叫 Dify Workflow 解析自然語言。
/// </summary>
public class LineWebhookService : ILineWebhookService
{
    private const string SchemaVersion = "1.0";
    private const string WorkflowVersion = "fixed-format-v1";
    private const string DifyWorkflowVersion = "dify-v1";

    /// <summary>待確認動作的有效期。</summary>
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(5);

    /// <summary>身分綁定碼的有效期。</summary>
    private static readonly TimeSpan BindingLifetime = TimeSpan.FromMinutes(10);

    /// <summary>行程未指定結束時間時的預設長度。</summary>
    private static readonly TimeSpan DefaultEventDuration = TimeSpan.FromHours(1);

    private readonly IMessagingRepository _messagingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IAccountEntryService _entryService;
    private readonly ICalendarEventService _calendarService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFixedFormatParser _parser;
    private readonly IDifyClient _difyClient;
    private readonly ICryptoHelper _cryptoHelper;
    private readonly TimeProvider _timeProvider;
    private readonly LineMessagingOptions _options;
    private readonly DifyOptions _difyOptions;
    private readonly ILogger<LineWebhookService> _logger;

    public LineWebhookService(
        IMessagingRepository messagingRepository,
        IUserRepository userRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceService workspaceService,
        IAccountEntryService entryService,
        ICalendarEventService calendarService,
        IUnitOfWork unitOfWork,
        IFixedFormatParser parser,
        IDifyClient difyClient,
        ICryptoHelper cryptoHelper,
        TimeProvider timeProvider,
        IOptions<LineMessagingOptions> options,
        IOptions<DifyOptions> difyOptions,
        ILogger<LineWebhookService> logger)
    {
        _messagingRepository = messagingRepository;
        _userRepository = userRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceService = workspaceService;
        _entryService = entryService;
        _calendarService = calendarService;
        _unitOfWork = unitOfWork;
        _parser = parser;
        _difyClient = difyClient;
        _cryptoHelper = cryptoHelper;
        _timeProvider = timeProvider;
        _options = options.Value;
        _difyOptions = difyOptions.Value;
        _logger = logger;
    }

    public async Task<string?> HandleMessageAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        // 事件冪等：LINE 會重送 Webhook，同一事件只處理一次
        var isNew = await _messagingRepository.TryMarkEventProcessedAsync(
            IdentityProvider.Line,
            message.EventId,
            cancellationToken);

        if (!isNew)
        {
            _logger.LogInformation("略過重複的 LINE 事件。");
            return null;
        }

        var user = await ResolveUserAsync(message.ExternalUserId, cancellationToken);
        var command = _parser.Parse(message.Text);

        var reply = user is null
            ? await HandleUnboundAsync(message.ExternalUserId, command, cancellationToken)
            : await HandleBoundAsync(user, message.Text, command, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return reply;
    }

    // -----------------------------------------------------------
    // 身分解析與綁定
    // -----------------------------------------------------------
    private async Task<User?> ResolveUserAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var hash = _cryptoHelper.Hash(externalUserId);

        // 先找這個 Messaging Channel 已建立的綁定
        var bound = await _userRepository.GetByExternalSubjectHashAsync(
            IdentityProvider.Line,
            _options.ChannelId,
            hash,
            cancellationToken);

        if (bound is not null)
        {
            return bound;
        }

        // LINE Login 與 Messaging 屬同一 Provider 時使用者 ID 相同，可直接沿用既有帳號
        if (!_options.SharesProviderWithLogin)
        {
            return null;
        }

        var existing = await _userRepository.GetByExternalSubjectHashAnyChannelAsync(
            IdentityProvider.Line,
            hash,
            cancellationToken);

        if (existing is not null)
        {
            await CreateIdentityAsync(existing.Id, externalUserId, hash, cancellationToken);
        }

        return existing;
    }

    private async Task<string> HandleUnboundAsync(
        string externalUserId,
        FixedCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Kind != FixedCommandKind.Binding || command.BindingCode is null)
        {
            return "還沒有綁定 NestFlow 帳號。\n請先用 LINE 登入 NestFlow，到「設定 → LINE 綁定」產生 6 碼綁定碼，再把綁定碼傳到這裡。";
        }

        var binding = await _messagingRepository.GetBindingCodeByHashAsync(
            _cryptoHelper.Hash(command.BindingCode),
            cancellationToken);

        var now = _timeProvider.GetUtcNow();

        // 不存在、已使用或已過期一律回相同訊息，避免用於探測
        if (binding is null || binding.Status != BindingCodeStatus.Pending || binding.UsedAt is not null)
        {
            return "綁定碼無效或已被使用，請重新產生。";
        }

        if (binding.ExpiresAt <= now)
        {
            binding.Status = BindingCodeStatus.Expired;
            return "綁定碼無效或已被使用，請重新產生。";
        }

        binding.Status = BindingCodeStatus.Used;
        binding.UsedAt = now;

        await CreateIdentityAsync(
            binding.UserId,
            externalUserId,
            _cryptoHelper.Hash(externalUserId),
            cancellationToken);

        return "綁定成功。\n現在可以直接傳訊息記帳，例如：\n記帳 午餐 120";
    }

    private async Task CreateIdentityAsync(
        Guid userId,
        string externalUserId,
        string hash,
        CancellationToken cancellationToken)
    {
        await _userRepository.AddExternalIdentityAsync(
            new ExternalIdentity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Provider = IdentityProvider.Line,
                ChannelId = _options.ChannelId,
                // 高敏感欄位加密後才存入，另存確定性雜湊供查找
                ExternalSubject = _cryptoHelper.Encrypt(externalUserId)!,
                ExternalSubjectHash = hash,
                Status = ExternalIdentityStatus.Active,
                CreatedAt = _timeProvider.GetUtcNow(),
            },
            cancellationToken);
    }

    // -----------------------------------------------------------
    // 已綁定使用者的指令處理
    // -----------------------------------------------------------
    private async Task<string> HandleBoundAsync(
        User user,
        string text,
        FixedCommand command,
        CancellationToken cancellationToken)
    {
        return command.Kind switch
        {
            FixedCommandKind.Entry => await CreatePendingEntryAsync(user, command, WorkflowVersion, cancellationToken),
            FixedCommandKind.Event => await CreatePendingEventAsync(user, command, WorkflowVersion, cancellationToken),
            FixedCommandKind.Confirm => await ConfirmAsync(user, cancellationToken),
            FixedCommandKind.Cancel => await CancelAsync(user, cancellationToken),
            FixedCommandKind.None => await HandleUnparsedAsync(user, text, cancellationToken),
            _ => BuildUsageText(),
        };
    }

    /// <summary>
    /// 固定格式解析不出結果時的退路：呼叫 Dify Workflow 解析自然語言。
    /// Dify 未設定、呼叫失敗或結果無效時，安全退回既有用法提示。
    /// </summary>
    private async Task<string> HandleUnparsedAsync(User user, string text, CancellationToken cancellationToken)
    {
        var command = await TryParseWithDifyAsync(user, text, cancellationToken);

        if (command is null)
        {
            return BuildUsageText();
        }

        return command.Kind switch
        {
            FixedCommandKind.Entry => await CreatePendingEntryAsync(user, command, DifyWorkflowVersion, cancellationToken),
            FixedCommandKind.Event => await CreatePendingEventAsync(user, command, DifyWorkflowVersion, cancellationToken),
            _ => BuildUsageText(),
        };
    }

    private async Task<FixedCommand?> TryParseWithDifyAsync(
        User user,
        string text,
        CancellationToken cancellationToken)
    {
        if (!_difyOptions.IsConfigured)
        {
            return null;
        }

        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(user.TimeZone, out var found)
            ? found
            : TimeZoneInfo.Utc;
        var nowLocal = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), zone);

        var request = new DifyParseRequest(
            text,
            nowLocal.ToString("yyyy-MM-dd"),
            nowLocal.ToString("HH:mm"),
            user.TimeZone,
            DifyCategoryCatalog.ExpenseCategoriesJson,
            DifyCategoryCatalog.IncomeCategoriesJson,
            user.Id.ToString());

        // Dify 只負責解析，不直接寫入資料庫；未知分類代碼、金額、日期時間與信心值一律由這裡驗證
        var result = await _difyClient.ParseAsync(request, cancellationToken);

        return DifyResultMapper.ToFixedCommand(result, _difyOptions.MinConfidence);
    }

    private async Task<string> CreatePendingEntryAsync(
        User user,
        FixedCommand command,
        string workflowVersion,
        CancellationToken cancellationToken)
    {
        var (workspaceId, workspaceName, error) = await ResolveWorkspaceAsync(user, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var now = _timeProvider.GetUtcNow();

        var payload = new PendingEntryPayload(
            command.Type!.Value.ToString(),
            command.Amount!.Value,
            SupportedCurrencies.Default,
            command.Category!,
            command.Note,
            now);

        var superseded = await AddPendingAsync(
            user,
            workspaceId!.Value,
            command.Type == EntryType.Expense
                ? PendingActionType.CreateExpense
                : PendingActionType.CreateIncome,
            JsonSerializer.Serialize(payload),
            workflowVersion,
            now,
            cancellationToken);

        return BuildEntryConfirmationText(command, workspaceName, superseded);
    }

    private async Task<string> CreatePendingEventAsync(
        User user,
        FixedCommand command,
        string workflowVersion,
        CancellationToken cancellationToken)
    {
        var parsed = command.Event!;

        var (workspaceId, workspaceName, error) = await ResolveWorkspaceAsync(user, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        if (!TryResolvePeriod(user, parsed, out var startAt, out var endAt))
        {
            return BuildEventUsageText("看不懂行程的日期或時間。");
        }

        var now = _timeProvider.GetUtcNow();
        var payload = new PendingEventPayload(parsed.Title, startAt, endAt);

        var superseded = await AddPendingAsync(
            user,
            workspaceId!.Value,
            PendingActionType.CreateCalendarEvent,
            JsonSerializer.Serialize(payload),
            workflowVersion,
            now,
            cancellationToken);

        return BuildEventConfirmationText(payload, user.TimeZone, workspaceName, superseded);
    }

    /// <summary>取得使用者的預設資料空間並確認仍可存取。無法使用時回傳要顯示的訊息。</summary>
    private async Task<(Guid? WorkspaceId, string WorkspaceName, string? Error)> ResolveWorkspaceAsync(
        User user,
        CancellationToken cancellationToken)
    {
        if (user.DefaultWorkspaceId is null)
        {
            return (null, string.Empty,
                "還沒有設定預設資料空間。\n請到 NestFlow 的「設定 → 預設資料空間」選一個之後再試。");
        }

        var workspaceId = user.DefaultWorkspaceId.Value;

        try
        {
            await _workspaceService.EnsureMemberAsync(user.Id, workspaceId, cancellationToken);
        }
        catch (AppException)
        {
            return (null, string.Empty, "預設資料空間已無法存取，請到 NestFlow 重新設定。");
        }

        var workspace = await _workspaceRepository.GetActiveAsync(workspaceId, cancellationToken);

        return (workspaceId, workspace?.Name ?? "預設資料空間", null);
    }

    /// <summary>建立待確認動作，並回傳是否取代了先前那一筆。</summary>
    private async Task<bool> AddPendingAsync(
        User user,
        Guid workspaceId,
        PendingActionType actionType,
        string payloadJson,
        string workflowVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // 一位使用者同時只保留一筆待確認，避免「確認」語意含糊
        var superseded = await _messagingRepository.SupersedePendingActionsAsync(
            user.Id,
            IdentityProvider.Line,
            cancellationToken);

        await _messagingRepository.AddPendingActionAsync(
            new PendingAction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                WorkspaceId = workspaceId,
                Provider = IdentityProvider.Line,
                ActionType = actionType,
                PayloadJson = payloadJson,
                SchemaVersion = SchemaVersion,
                WorkflowVersion = workflowVersion,
                Status = PendingActionStatus.Pending,
                ExpiresAt = now.Add(PendingLifetime),
                CreatedAt = now,
            },
            cancellationToken);

        return superseded > 0;
    }

    /// <summary>
    /// 把解析出的字面日期時間換算為 UTC。
    /// 未指定日期視為今天，未指定結束時間預設一小時，結束早於開始視為跨過午夜。
    /// </summary>
    private bool TryResolvePeriod(
        User user,
        ParsedEvent parsed,
        out DateTimeOffset startUtc,
        out DateTimeOffset endUtc)
    {
        startUtc = default;
        endUtc = default;

        if (parsed.StartHour > 23 || parsed.StartMinute > 59)
        {
            return false;
        }

        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(user.TimeZone, out var found)
            ? found
            : TimeZoneInfo.Utc;

        var nowLocal = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), zone);

        DateTime localDate;

        if (parsed.Month is { } month && parsed.Day is { } day)
        {
            if (month is < 1 or > 12)
            {
                return false;
            }

            var year = nowLocal.Year;

            if (day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                return false;
            }

            localDate = new DateTime(year, month, day);

            // 只給月日時，若日期已過就視為明年
            if (localDate < nowLocal.Date)
            {
                if (day > DateTime.DaysInMonth(year + 1, month))
                {
                    return false;
                }

                localDate = new DateTime(year + 1, month, day);
            }
        }
        else
        {
            localDate = nowLocal.Date.AddDays(parsed.DayOffset ?? 0);
        }

        var start = localDate.AddHours(parsed.StartHour).AddMinutes(parsed.StartMinute);
        DateTime end;

        if (parsed.EndHour is { } endHour && parsed.EndMinute is { } endMinute)
        {
            if (endHour > 23 || endMinute > 59)
            {
                return false;
            }

            end = localDate.AddHours(endHour).AddMinutes(endMinute);

            if (end <= start)
            {
                end = end.AddDays(1);
            }
        }
        else
        {
            end = start.Add(DefaultEventDuration);
        }

        try
        {
            startUtc = ToUtc(start, zone);
            endUtc = ToUtc(end, zone);
        }
        catch (ArgumentException)
        {
            // 日光節約時間造成的無效當地時間
            return false;
        }

        return true;
    }

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);

        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private async Task<string> ConfirmAsync(User user, CancellationToken cancellationToken)
    {
        var pending = await _messagingRepository.GetPendingActionAsync(
            user.Id,
            IdentityProvider.Line,
            cancellationToken);

        if (pending is null)
        {
            return "目前沒有待確認的項目。\n可以先傳「記帳 午餐 120」或「行程 明天 14:00 開會」再回覆「確認」。";
        }

        return pending.ActionType == PendingActionType.CreateCalendarEvent
            ? await ConfirmEventAsync(user, pending, cancellationToken)
            : await ConfirmEntryAsync(pending, cancellationToken);
    }

    private async Task<string> ConfirmEntryAsync(PendingAction pending, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PendingEntryPayload>(pending.PayloadJson);

        if (payload is null)
        {
            pending.Status = PendingActionStatus.Cancelled;
            return "這筆待確認的資料有誤，已取消，請重新輸入。";
        }

        var type = payload.Type == nameof(EntryType.Income) ? EntryType.Income : EntryType.Expense;

        // 寫入一律走與 PWA 相同的服務，權限與驗證邏輯完全一致
        await _entryService.CreateAsync(
            pending.UserId,
            pending.WorkspaceId,
            new SaveAccountEntryCommand(
                type,
                payload.Amount,
                payload.Currency,
                payload.Category,
                payload.Note,
                payload.OccurredAt,
                "full",
                []),
            cancellationToken);

        pending.Status = PendingActionStatus.Confirmed;

        var label = AccountCategories.All.First(x => x.Code == payload.Category).Label;

        return $"已記錄{(type == EntryType.Expense ? "支出" : "收入")}：{label} {payload.Amount:0.##} 元。";
    }

    private async Task<string> ConfirmEventAsync(
        User user,
        PendingAction pending,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<PendingEventPayload>(pending.PayloadJson);

        if (payload is null)
        {
            pending.Status = PendingActionStatus.Cancelled;
            return "這筆待確認的資料有誤，已取消，請重新輸入。";
        }

        await _calendarService.CreateAsync(
            pending.UserId,
            pending.WorkspaceId,
            new SaveCalendarEventCommand(payload.Title, null, payload.StartAt, payload.EndAt),
            cancellationToken);

        pending.Status = PendingActionStatus.Confirmed;

        return $"已加入行程：{payload.Title}\n{FormatPeriod(payload, user.TimeZone)}";
    }

    private async Task<string> CancelAsync(User user, CancellationToken cancellationToken)
    {
        var pending = await _messagingRepository.GetPendingActionAsync(
            user.Id,
            IdentityProvider.Line,
            cancellationToken);

        if (pending is null)
        {
            return "目前沒有待確認的項目。";
        }

        pending.Status = PendingActionStatus.Cancelled;

        return "已取消，沒有寫入任何資料。";
    }

    // -----------------------------------------------------------
    // 回覆文字
    // -----------------------------------------------------------
    private static string BuildEntryConfirmationText(FixedCommand command, string workspaceName, bool superseded)
    {
        var label = AccountCategories.All.First(x => x.Code == command.Category).Label;
        var typeText = command.Type == EntryType.Expense ? "支出" : "收入";

        var lines = new List<string>();

        if (superseded)
        {
            lines.Add("上一筆待確認的記帳已取消。");
        }

        lines.Add($"準備新增{typeText}");
        lines.Add($"金額：{command.Amount:0.##} 元");
        lines.Add($"分類：{label}");

        if (!string.IsNullOrWhiteSpace(command.Note))
        {
            lines.Add($"備註：{command.Note}");
        }

        lines.Add($"資料空間：{workspaceName}");

        if (command.CategoryIsFallback)
        {
            lines.Add("（找不到對應分類，已歸為其他）");
        }

        lines.Add(string.Empty);
        lines.Add("回覆「確認」寫入，或回覆「取消」放棄。5 分鐘內有效。");

        return string.Join('\n', lines);
    }

    private static string BuildEventConfirmationText(
        PendingEventPayload payload,
        string timeZone,
        string workspaceName,
        bool superseded)
    {
        var lines = new List<string>();

        if (superseded)
        {
            lines.Add("上一筆待確認的項目已取消。");
        }

        lines.Add("準備新增行程");
        lines.Add($"標題：{payload.Title}");
        lines.Add($"時間：{FormatPeriod(payload, timeZone)}");
        lines.Add($"資料空間：{workspaceName}");
        lines.Add(string.Empty);
        lines.Add("回覆「確認」寫入，或回覆「取消」放棄。5 分鐘內有效。");

        return string.Join('\n', lines);
    }

    /// <summary>以使用者時區顯示行程時間，例如「8/10（週一）14:00 – 15:30」。</summary>
    private static string FormatPeriod(PendingEventPayload payload, string timeZone)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var found)
            ? found
            : TimeZoneInfo.Utc;

        var start = TimeZoneInfo.ConvertTime(payload.StartAt, zone);
        var end = TimeZoneInfo.ConvertTime(payload.EndAt, zone);

        var weekdays = new[] { "日", "一", "二", "三", "四", "五", "六" };
        var head = $"{start.Month}/{start.Day}（週{weekdays[(int)start.DayOfWeek]}）{start:HH:mm}";

        // 跨日行程要把結束日期一併寫出，只顯示時間會誤導
        return start.Date == end.Date
            ? $"{head} – {end:HH:mm}"
            : $"{head} – {end.Month}/{end.Day} {end:HH:mm}";
    }

    private static string BuildUsageText()
    {
        return string.Join(
            '\n',
            "可以這樣記帳：",
            "記帳 午餐 120",
            "支出 交通 60 捷運",
            "收入 薪資 50000",
            string.Empty,
            "也可以這樣排行程：",
            "行程 明天 14:00 開會",
            "行程 8/10 14:00-15:30 專案會議",
            string.Empty,
            "傳出後回覆「確認」才會寫入，回覆「取消」則放棄。");
    }

    private static string BuildEventUsageText(string reason)
    {
        return string.Join(
            '\n',
            reason,
            "可以這樣傳：",
            "行程 明天 14:00 開會",
            "行程 8/10 14:00 看牙醫",
            "行程 8/10 14:00-15:30 專案會議",
            string.Empty,
            "不指定日期就是今天，不指定結束時間就是一小時。");
    }

    /// <summary>待確認記帳的內容。以 JSON 保存於 pending_actions.payload_json。</summary>
    private record PendingEntryPayload(
        string Type,
        decimal Amount,
        string Currency,
        string Category,
        string? Note,
        DateTimeOffset OccurredAt);

    /// <summary>待確認行程的內容。時間已換算為 UTC。</summary>
    private record PendingEventPayload(
        string Title,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt);
}
