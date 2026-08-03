using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

/// <summary>
/// 外部通訊軟體整合所需的資料存取：事件冪等、待確認動作與身分綁定碼。
/// </summary>
public interface IMessagingRepository
{
    /// <summary>
    /// 嘗試登記外部事件。已存在時回傳 false，代表這是重送的事件不應再處理。
    /// </summary>
    Task<bool> TryMarkEventProcessedAsync(
        IdentityProvider provider,
        string externalEventId,
        CancellationToken cancellationToken);

    Task AddPendingActionAsync(PendingAction action, CancellationToken cancellationToken);

    /// <summary>取得使用者在該來源上尚待確認且未過期的動作。</summary>
    Task<PendingAction?> GetPendingActionAsync(
        Guid userId,
        IdentityProvider provider,
        CancellationToken cancellationToken);

    /// <summary>把使用者既有的待確認動作標記為被新動作取代。</summary>
    Task<int> SupersedePendingActionsAsync(
        Guid userId,
        IdentityProvider provider,
        CancellationToken cancellationToken);

    Task AddBindingCodeAsync(BindingCode code, CancellationToken cancellationToken);

    Task<BindingCode?> GetBindingCodeByHashAsync(string codeHash, CancellationToken cancellationToken);
}
