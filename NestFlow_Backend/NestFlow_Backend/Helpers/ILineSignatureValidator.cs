namespace NestFlow_Backend.Helpers;

/// <summary>
/// 驗證 LINE Webhook 的 X-Line-Signature。純函數，不存取資料庫。
/// </summary>
public interface ILineSignatureValidator
{
    bool IsValid(string channelSecret, ReadOnlySpan<byte> body, string? signature);
}
