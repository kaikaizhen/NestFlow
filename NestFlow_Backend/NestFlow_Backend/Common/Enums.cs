namespace NestFlow_Backend.Common;

/// <summary>使用者狀態。</summary>
public enum UserStatus
{
    Active,
    Disabled,
}

/// <summary>外部身分提供者。第一版只使用 LINE。</summary>
public enum IdentityProvider
{
    Line,
}

/// <summary>外部身分綁定狀態。</summary>
public enum ExternalIdentityStatus
{
    Active,
    Revoked,
}

/// <summary>Workspace 類型。第一版只允許個人與家庭。</summary>
public enum WorkspaceType
{
    Personal,
    Family,
}

/// <summary>Workspace 狀態。刪除採軟刪除。</summary>
public enum WorkspaceStatus
{
    Active,
    Deleted,
}

/// <summary>成員身分。第一版只有建立者與成員。</summary>
public enum MembershipType
{
    Owner,
    Member,
}

/// <summary>成員狀態。移除與離開皆改為 Removed。</summary>
public enum MembershipStatus
{
    Active,
    Removed,
}

/// <summary>邀請碼狀態。</summary>
public enum InvitationStatus
{
    Pending,
    Used,
    Expired,
}
