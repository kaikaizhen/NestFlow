using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;
using NestFlow_Backend.Services.External;
using SessionOptions = NestFlow_Backend.Common.SessionOptions;

namespace NestFlow_Backend.Services;

public class AuthService : IAuthService
{
    private const string DevChannelId = "dev-channel";

    private readonly ILineLoginClient _lineLoginClient;
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICodeGenerator _codeGenerator;
    private readonly ICryptoHelper _cryptoHelper;
    private readonly TimeProvider _timeProvider;
    private readonly LineLoginOptions _lineOptions;
    private readonly SessionOptions _sessionOptions;

    public AuthService(
        ILineLoginClient lineLoginClient,
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IWorkspaceService workspaceService,
        IUnitOfWork unitOfWork,
        ICodeGenerator codeGenerator,
        ICryptoHelper cryptoHelper,
        TimeProvider timeProvider,
        IOptions<LineLoginOptions> lineOptions,
        IOptions<SessionOptions> sessionOptions)
    {
        _lineLoginClient = lineLoginClient;
        _userRepository = userRepository;
        _sessionRepository = sessionRepository;
        _workspaceService = workspaceService;
        _unitOfWork = unitOfWork;
        _codeGenerator = codeGenerator;
        _cryptoHelper = cryptoHelper;
        _timeProvider = timeProvider;
        _lineOptions = lineOptions.Value;
        _sessionOptions = sessionOptions.Value;
    }

    public (string AuthorizationUrl, string State, string Nonce) StartLineLogin()
    {
        var state = _codeGenerator.GenerateOAuthValue();
        var nonce = _codeGenerator.GenerateOAuthValue();

        return (_lineLoginClient.BuildAuthorizationUrl(state, nonce), state, nonce);
    }

    public async Task<string> CompleteLineLoginAsync(string code, string nonce, CancellationToken cancellationToken)
    {
        var profile = await _lineLoginClient.ExchangeCodeAsync(code, nonce, cancellationToken);

        var user = await FindOrCreateUserAsync(
            IdentityProvider.Line,
            _lineOptions.ChannelId,
            profile.Subject,
            profile.DisplayName,
            profile.PictureUrl,
            cancellationToken);

        return await IssueSessionAsync(user.Id, cancellationToken);
    }

    public async Task<string> DevLoginAsync(
        string externalSubject,
        string displayName,
        CancellationToken cancellationToken)
    {
        var user = await FindOrCreateUserAsync(
            IdentityProvider.Line,
            DevChannelId,
            externalSubject,
            displayName,
            pictureUrl: null,
            cancellationToken);

        return await IssueSessionAsync(user.Id, cancellationToken);
    }

    public async Task<Guid?> ResolveUserIdAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByTokenHashAsync(
            _cryptoHelper.Hash(sessionToken),
            cancellationToken);

        if (session is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        if (session.ExpiresAt <= now)
        {
            return null;
        }

        if (session.User is null || session.User.Status != UserStatus.Active)
        {
            return null;
        }

        // 滑動對期：剩餘效期低於門檻時才寫入資料庫，避免每次請求都更新
        var lifetime = TimeSpan.FromDays(_sessionOptions.LifetimeDays);
        var remaining = session.ExpiresAt - now;

        if (remaining < lifetime * _sessionOptions.SlidingRenewThreshold)
        {
            session.ExpiresAt = now.Add(lifetime);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return session.UserId;
    }

    public async Task<CurrentUserDtoModel> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized();

        var identity = await _userRepository.GetExternalIdentityAsync(userId, IdentityProvider.Line, cancellationToken);

        return new CurrentUserDtoModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            PictureUrl = user.PictureUrl,
            DefaultWorkspaceId = user.DefaultWorkspaceId,
            TimeZone = user.TimeZone,
            IsLineLinked = identity is not null && identity.ChannelId != DevChannelId,
        };
    }

    public async Task UpdateTimeZoneAsync(Guid userId, string timeZone, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.Unauthorized();

        // 只接受作業系統認得的時區，避免存入無法解析的字串
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _))
        {
            throw AppException.BadRequest("時區名稱無效。");
        }

        user.TimeZone = timeZone;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAsync(string sessionToken, CancellationToken cancellationToken)
    {
        await _sessionRepository.RevokeAsync(
            _cryptoHelper.Hash(sessionToken),
            _timeProvider.GetUtcNow(),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> FindOrCreateUserAsync(
        IdentityProvider provider,
        string channelId,
        string externalSubject,
        string displayName,
        string? pictureUrl,
        CancellationToken cancellationToken)
    {
        var subjectHash = _cryptoHelper.Hash(externalSubject);

        var existing = await _userRepository.GetByExternalSubjectHashAsync(
            provider,
            channelId,
            subjectHash,
            cancellationToken);

        if (existing is not null)
        {
            // 每次登入同步 LINE 上的顯示名稱與頭像
            existing.DisplayName = displayName;
            existing.PictureUrl = pictureUrl;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return existing;
        }

        var now = _timeProvider.GetUtcNow();

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            PictureUrl = pictureUrl,
            Status = UserStatus.Active,
            CreatedAt = now,
        };

        await _userRepository.AddAsync(user, cancellationToken);

        await _userRepository.AddExternalIdentityAsync(
            new ExternalIdentity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = provider,
                ChannelId = channelId,
                // 高敏感欄位加密後才存入，另存確定性雜湊供查找
                ExternalSubject = _cryptoHelper.Encrypt(externalSubject)!,
                ExternalSubjectHash = subjectHash,
                Status = ExternalIdentityStatus.Active,
                CreatedAt = now,
            },
            cancellationToken);

        // 首次登入自動建立預設個人 Workspace
        user.DefaultWorkspaceId = await _workspaceService.CreateDefaultPersonalWorkspaceAsync(
            user.Id,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }

    private async Task<string> IssueSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var token = _codeGenerator.GenerateSessionToken();
        var now = _timeProvider.GetUtcNow();

        await _sessionRepository.AddAsync(
            new Session
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                // 只保存雜湊，明文僅回給瀏覽器
                TokenHash = _cryptoHelper.Hash(token),
                ExpiresAt = now.AddDays(_sessionOptions.LifetimeDays),
                CreatedAt = now,
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return token;
    }
}
