using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Dtos;

public class WorkspaceDtoModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public WorkspaceType Type { get; set; }

    public MembershipType MembershipType { get; set; }

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class WorkspaceMemberDtoModel
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    public MembershipType MembershipType { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}

public class InvitationDtoModel
{
    /// <summary>邀請碼明文，只在產生當下回傳一次，資料庫僅保存雜湊。</summary>
    public string Code { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>LINE 官方帳號資訊，供 PWA 顯示加好友入口。未設定 Channel 時為 null。</summary>
public class LineBotDtoModel
{
    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    /// <summary>加好友連結。手機開啟會直接跳到 LINE，電腦開啟則顯示 QR Code。</summary>
    public string AddFriendUrl { get; set; } = string.Empty;
}

/// <summary>身分綁定碼。明文只在產生當下回傳一次，資料庫僅保存雜湊。</summary>
public class BindingCodeDtoModel
{
    public string Code { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}

public class CurrentUserDtoModel
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PictureUrl { get; set; }

    public Guid? DefaultWorkspaceId { get; set; }

    /// <summary>使用者時區的 IANA 名稱，前端據此換算顯示時間與月份區間。</summary>
    public string TimeZone { get; set; } = string.Empty;

    public bool IsLineLinked { get; set; }

    /// <summary>是否已綁定 LINE 官方帳號（Messaging Channel），綁定後才能用 LINE 記帳。</summary>
    public bool IsLineMessagingLinked { get; set; }

    /// <summary>後端是否已設定 Messaging Channel。未設定時前端不顯示綁定入口。</summary>
    public bool IsLineMessagingConfigured { get; set; }
}
