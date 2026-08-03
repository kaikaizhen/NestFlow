namespace NestFlow_Backend.Services.External;

/// <summary>官方帳號的公開資訊，供 PWA 顯示加好友入口。</summary>
public record LineBotInfo(string BasicId, string DisplayName, string? PictureUrl);

/// <summary>
/// LINE Messaging API 對外呼叫。抽為介面以便測試時替換。
/// </summary>
public interface ILineMessagingClient
{
    /// <summary>以 replyToken 回覆訊息。權杖只能用一次且有時效。</summary>
    Task ReplyAsync(string replyToken, string text, CancellationToken cancellationToken);

    /// <summary>
    /// 主動推播訊息給指定使用者。回傳是否送達，失敗時由呼叫端決定重試或放棄。
    /// </summary>
    Task<bool> PushAsync(string externalUserId, string text, CancellationToken cancellationToken);

    /// <summary>取得官方帳號資訊。尚未設定或呼叫失敗時回傳 null。</summary>
    Task<LineBotInfo?> GetBotInfoAsync(CancellationToken cancellationToken);
}
