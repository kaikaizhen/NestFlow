namespace NestFlow_Worker;

/// <summary>
/// 背景服務空殼。Module 1 只驗證 Worker 容器可正常啟動與存活，
/// 實際的提醒排程與 LINE Push 於 Module 6 實作。
/// </summary>
public class Worker : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NestFlow Worker 已啟動。");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("NestFlow Worker 心跳：{Time}", DateTimeOffset.Now);

            try
            {
                await Task.Delay(HeartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("NestFlow Worker 已停止。");
    }
}
