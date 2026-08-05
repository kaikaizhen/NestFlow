namespace NestFlow_Backend.Tests;

/// <summary>
/// 以真實時間為基準、可整體前移的 TimeProvider。
/// 預設 Offset 為零，行為與 TimeProvider.System 相同，不影響其他測試；
/// 提醒測試把時間往前推，就不必真的等到觸發時間才能驗證派送。
/// </summary>
public class TestTimeProvider : TimeProvider
{
    public TimeSpan Offset { get; set; } = TimeSpan.Zero;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.Add(Offset);
}
