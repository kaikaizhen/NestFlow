using NestFlow_Backend.Services.External;

namespace NestFlow_Backend.Tests;

/// <summary>
/// 測試用的 Dify 用戶端。不對外連線，回傳事先設定好的結果，
/// 並記錄最後一次呼叫的請求內容供斷言。
/// </summary>
public class FakeDifyClient : IDifyClient
{
    /// <summary>下一次呼叫要回傳的結果。null 代表模擬呼叫失敗或尚未設定。</summary>
    public DifyCommandResult? NextResult { get; set; }

    public DifyParseRequest? LastRequest { get; private set; }

    public void Reset()
    {
        NextResult = null;
        LastRequest = null;
    }

    public Task<DifyCommandResult?> ParseAsync(DifyParseRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;

        return Task.FromResult(NextResult);
    }
}
