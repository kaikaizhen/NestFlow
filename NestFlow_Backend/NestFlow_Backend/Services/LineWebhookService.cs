using System.Text.Json;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;

namespace NestFlow_Backend.Services;

/// <summary>
/// LINE 固定格式記帳流程。
/// 訊息只會產生待確認動作，使用者回覆「確認」後才真正寫入資料。
/// </summary>
public class LineWebhookService : ILineWebhookService
{
    private const string SchemaVersion = "1.0";
    private const string WorkflowVersion = "fixed-format-v1";

    /// <summary>待確認動作的有效期。</summary>
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(5);

    /// <summary>身分綁定碼的有效期。</summary>
    private static readonly TimeSpan BindingLifetime = TimeSpan.FromMinutes(10);

    private readonly IMessagingRepository _messagingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IAccountEntryService _entryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFixedFormatParser _parser;
    private readonly ICryptoHelper _cryptoHelper;
    private readonly TimeProvider _timeProvider;
    private readonly LineMessagingOptions _options;
    private readonly ILogger<LineWebhookService> _logger;

    public LineWebhookService(
        IMessagingRepository messagingRepository,
        IUserRepository userRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceService workspaceService,
        IAccountEntryService entryService,
        IUnitOfWork unitOfWork,
        IFixedFormatParser parser,
        ICryptoHelper cryptoHelper,
        TimeProvider timeProvider,
        IOptions<LineMessagingOptions> options,
        ILogger<LineWebhookService> logger)
    {
        _messagingRepository = messagingRepository;
        _userRepository = userRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceService = workspaceService;
        _entryService = entryService;
        _unitOfWork = unitOfWork;
        _parser = parser;
        _cryptoHelper = cryptoHelper;
        _timeProvider = timeProvider;
        _options = options.Value;
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
            : await HandleBoundAsync(user, command, cancellationToken);

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
        FixedCommand command,
        CancellationToken cancellationToken)
    {
        return command.Kind switch
        {
            FixedCommandKind.Entry => await CreatePendingAsync(user, command, cancellationToken),
            FixedCommandKind.Confirm => await ConfirmAsync(user, cancellationToken),
            FixedCommandKind.Cancel => await CancelAsync(user, cancellationToken),
            _ => BuildUsageText(),
        };
    }

    private async Task<string> CreatePendingAsync(
        User user,
        FixedCommand command,
        CancellationToken cancellationToken)
    {
        if (user.DefaultWorkspaceId is null)
        {
            return "還沒有設定預設資料空間。\n請到 NestFlow 的「設定 → 預設資料空間」選一個之後再試。";
        }

        var workspaceId = user.DefaultWorkspaceId.Value;

        try
        {
            await _workspaceService.EnsureMemberAsync(user.Id, workspaceId, cancellationToken);
        }
        catch (AppException)
        {
            return "預設資料空間已無法存取，請到 NestFlow 重新設定。";
        }

        var workspace = await _workspaceRepository.GetActiveAsync(workspaceId, cancellationToken);

        // 一位使用者同時只保留一筆待確認，避免「確認」語意含糊
        var superseded = await _messagingRepository.SupersedePendingActionsAsync(
            user.Id,
            IdentityProvider.Line,
            cancellationToken);

        var now = _timeProvider.GetUtcNow();

        var payload = new PendingEntryPayload(
            command.Type!.Value.ToString(),
            command.Amount!.Value,
            SupportedCurrencies.Default,
            command.Category!,
            command.Note,
            now);

        await _messagingRepository.AddPendingActionAsync(
            new PendingAction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                WorkspaceId = workspaceId,
                Provider = IdentityProvider.Line,
                ActionType = command.Type == EntryType.Expense
                    ? PendingActionType.CreateExpense
                    : PendingActionType.CreateIncome,
                PayloadJson = JsonSerializer.Serialize(payload),
                SchemaVersion = SchemaVersion,
                WorkflowVersion = WorkflowVersion,
                Status = PendingActionStatus.Pending,
                ExpiresAt = now.Add(PendingLifetime),
                CreatedAt = now,
            },
            cancellationToken);

        return BuildConfirmationText(command, workspace?.Name ?? "預設資料空間", superseded > 0);
    }

    private async Task<string> ConfirmAsync(User user, CancellationToken cancellationToken)
    {
        var pending = await _messagingRepository.GetPendingActionAsync(
            user.Id,
            IdentityProvider.Line,
            cancellationToken);

        if (pending is null)
        {
            return "目前沒有待確認的記帳。\n可以先傳「記帳 午餐 120」再回覆「確認」。";
        }

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
                payload.OccurredAt),
            cancellationToken);

        pending.Status = PendingActionStatus.Confirmed;

        var label = AccountCategories.All.First(x => x.Code == payload.Category).Label;

        return $"已記錄{(type == EntryType.Expense ? "支出" : "收入")}：{label} {payload.Amount:0.##} 元。";
    }

    private async Task<string> CancelAsync(User user, CancellationToken cancellationToken)
    {
        var pending = await _messagingRepository.GetPendingActionAsync(
            user.Id,
            IdentityProvider.Line,
            cancellationToken);

        if (pending is null)
        {
            return "目前沒有待確認的記帳。";
        }

        pending.Status = PendingActionStatus.Cancelled;

        return "已取消，沒有寫入任何資料。";
    }

    // -----------------------------------------------------------
    // 回覆文字
    // -----------------------------------------------------------
    private static string BuildConfirmationText(FixedCommand command, string workspaceName, bool superseded)
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

    private static string BuildUsageText()
    {
        return string.Join(
            '\n',
            "可以這樣記帳：",
            "記帳 午餐 120",
            "支出 交通 60 捷運",
            "收入 薪資 50000",
            string.Empty,
            "傳出後回覆「確認」才會寫入，回覆「取消」則放棄。");
    }

    /// <summary>待確認記帳的內容。以 JSON 保存於 pending_actions.payload_json。</summary>
    private record PendingEntryPayload(
        string Type,
        decimal Amount,
        string Currency,
        string Category,
        string? Note,
        DateTimeOffset OccurredAt);
}
