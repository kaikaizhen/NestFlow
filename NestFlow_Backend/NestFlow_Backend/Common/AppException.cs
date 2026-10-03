using Library.Http;

namespace NestFlow_Backend.Common;

/// <summary>
/// 商業規則違反時拋出，由全域例外處理轉為對應的 HTTP 狀態碼。
/// </summary>
public class AppException : HttpException
{
    public AppException(int statusCode, string message)
        : base((System.Net.HttpStatusCode)statusCode, message)
    {
    }

    /// <summary>保留既有服務與測試使用的數字型態狀態碼。</summary>
    public new int StatusCode => (int)base.StatusCode;

    public static AppException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);

    public static AppException Unauthorized(string message = "尚未登入。") => new(StatusCodes.Status401Unauthorized, message);

    /// <summary>
    /// 無權存取。為避免洩漏資源是否存在，非成員存取 Workspace 一律回傳 404。
    /// </summary>
    public static AppException NotFound(string message = "找不到資料或沒有存取權限。") => new(StatusCodes.Status404NotFound, message);

    public static AppException Forbidden(string message = "沒有執行此操作的權限。") => new(StatusCodes.Status403Forbidden, message);

    public static AppException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
}
