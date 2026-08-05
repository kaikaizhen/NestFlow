using NestFlow_Backend.Services.External;

namespace NestFlow_Backend.Tests;

/// <summary>
/// 測試用的 LINE 用戶端。不對外連線，只記錄呼叫內容，
/// 並可切換推播成敗以驗證重試與失敗處理。
/// </summary>
public class FakeLineMessagingClient : ILineMessagingClient
{
    private readonly List<(string To, string Text)> _pushes = [];

    /// <summary>設為 false 可模擬推播失敗，用於驗證重試與最終失敗。</summary>
    public bool PushSucceeds { get; set; } = true;

    public IReadOnlyList<(string To, string Text)> Pushes
    {
        get
        {
            lock (_pushes)
            {
                return _pushes.ToList();
            }
        }
    }

    public void Reset()
    {
        lock (_pushes)
        {
            _pushes.Clear();
        }

        PushSucceeds = true;
    }

    public Task ReplyAsync(string replyToken, string text, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<bool> PushAsync(string externalUserId, string text, CancellationToken cancellationToken)
    {
        if (!PushSucceeds)
        {
            return Task.FromResult(false);
        }

        lock (_pushes)
        {
            _pushes.Add((externalUserId, text));
        }

        return Task.FromResult(true);
    }

    public Task<LineBotInfo?> GetBotInfoAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<LineBotInfo?>(new LineBotInfo("@nestflow", "NestFlow 測試帳號", null));
    }
}
