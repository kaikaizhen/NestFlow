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

/// <summary>記帳類型。</summary>
public enum EntryType
{
    Expense,
    Income,
}

/// <summary>記帳狀態。刪除採軟刪除，不從資料庫移除。</summary>
public enum EntryStatus
{
    Active,
    Deleted,
}

/// <summary>行程狀態。刪除採軟刪除，不從資料庫移除。</summary>
public enum CalendarEventStatus
{
    Active,
    Deleted,
}

/// <summary>代辦類型。第一版只有一般代辦與購物清單。</summary>
public enum TodoType
{
    General,
    Shopping,
}

/// <summary>
/// 代辦狀態。刪除採軟刪除，不從資料庫移除。
/// 是否完成不放在這裡，改以 completed_at 是否有值表示，兩者互不影響。
/// </summary>
public enum TodoStatus
{
    Active,
    Deleted,
}

/// <summary>
/// 提醒狀態。Sending 是 Worker 取件後的暫時狀態，
/// 讓多個 Worker 同時掃描時同一筆提醒只會有一個取得。
/// </summary>
public enum ReminderStatus
{
    Pending,
    Sending,
    Sent,
    Failed,
    Cancelled,

    /// <summary>
    /// 到期時使用者已關閉「提醒通知」總開關，因此略過發送。
    /// 屬終態，重新開啟總開關後不會補發，避免收到一堆過期通知。
    /// </summary>
    Skipped,
}

/// <summary>週期行程的結束方式。只在建立時使用，建立後規則不可變更。</summary>
public enum CalendarRecurrenceEndType
{
    /// <summary>重複固定次數。</summary>
    Count,

    /// <summary>重複到指定日期為止。</summary>
    UntilDate,

    /// <summary>不設結束日，實作上仍會限制在 2 年內以避免無限生成。</summary>
    Forever,
}

/// <summary>待確認動作的類型。</summary>
public enum PendingActionType
{
    CreateExpense,
    CreateIncome,
    CreateCalendarEvent,
}

/// <summary>待確認動作的狀態。</summary>
public enum PendingActionStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Superseded,
    Expired,
}

/// <summary>外部事件處理狀態，用於冪等。</summary>
public enum ProcessedEventStatus
{
    Processed,
}

/// <summary>身分綁定碼狀態。</summary>
public enum BindingCodeStatus
{
    Pending,
    Used,
    Expired,
}
