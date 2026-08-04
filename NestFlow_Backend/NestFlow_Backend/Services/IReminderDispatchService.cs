namespace NestFlow_Backend.Services;

/// <summary>單次派送的結果，供 Worker 記錄與測試斷言。</summary>
public record ReminderDispatchResult(int Claimed, int Sent, int Retrying, int Failed, int Skipped = 0);

/// <summary>
/// 提醒派送。由 Background Worker 定期呼叫，也可在開發環境手動觸發。
/// </summary>
public interface IReminderDispatchService
{
    Task<ReminderDispatchResult> DispatchDueAsync(CancellationToken cancellationToken);
}
