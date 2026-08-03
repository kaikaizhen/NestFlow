using NestFlow_Backend.Services;

namespace NestFlow_Worker;

/// <summary>
/// 提醒排程。固定間隔掃描到期的提醒並交由派送服務送出。
/// 取件是原子性的，因此就算跑多個 Worker 實例也不會重複發送。
/// </summary>
public class ReminderWorker : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);

    /// <summary>資料庫尚未就緒時的退避間隔，避免啟動瞬間狂刷錯誤日誌。</summary>
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderWorker> _logger;

    public ReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NestFlow 提醒 Worker 已啟動，每 {Seconds} 秒掃描一次。", ScanInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ScanInterval;

            try
            {
                var result = await DispatchAsync(stoppingToken);

                if (result.Claimed > 0)
                {
                    _logger.LogInformation(
                        "提醒派送完成：取出 {Claimed}、送出 {Sent}、待重試 {Retrying}、失敗 {Failed}。",
                        result.Claimed,
                        result.Sent,
                        result.Retrying,
                        result.Failed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 單次失敗不應讓 Worker 結束，退避後再試
                _logger.LogError(ex, "提醒派送發生錯誤，將於 {Seconds} 秒後重試。", ErrorBackoff.TotalSeconds);
                delay = ErrorBackoff;
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("NestFlow 提醒 Worker 已停止。");
    }

    private async Task<ReminderDispatchResult> DispatchAsync(CancellationToken cancellationToken)
    {
        // 每輪開新的 Scope，DbContext 不跨輪重用
        using var scope = _scopeFactory.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IReminderDispatchService>();

        return await dispatcher.DispatchDueAsync(cancellationToken);
    }
}
