namespace NestFlow_Backend.Common;

/// <summary>
/// 目前登入使用者。UserId 一律由 Session 解析，永不接受前端傳入。
/// </summary>
public interface ICurrentUserAccessor
{
    Guid? UserId { get; }

    /// <summary>取得目前使用者，未登入時拋出 401。</summary>
    Guid RequireUserId();
}

public class CurrentUserAccessor : ICurrentUserAccessor
{
    public const string HttpContextItemKey = "NestFlow.CurrentUserId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var items = _httpContextAccessor.HttpContext?.Items;

            if (items is not null && items.TryGetValue(HttpContextItemKey, out var value) && value is Guid userId)
            {
                return userId;
            }

            return null;
        }
    }

    public Guid RequireUserId() => UserId ?? throw AppException.Unauthorized();
}
