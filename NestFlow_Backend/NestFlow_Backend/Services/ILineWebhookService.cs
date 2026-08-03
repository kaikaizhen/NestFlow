namespace NestFlow_Backend.Services;

/// <summary>從 LINE Webhook 取出、已正規化的單一訊息事件。</summary>
public record IncomingMessage(
    string EventId,
    string ExternalUserId,
    string Text,
    string? ReplyToken);

public interface ILineWebhookService
{
    /// <summary>
    /// 處理一則訊息並回傳要回覆給使用者的文字。
    /// 事件重複、非文字訊息等情況回傳 null，代表不需回覆。
    /// </summary>
    Task<string?> HandleMessageAsync(IncomingMessage message, CancellationToken cancellationToken);
}
