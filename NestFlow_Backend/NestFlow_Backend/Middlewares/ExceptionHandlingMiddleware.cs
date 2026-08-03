using System.Text.Json;
using NestFlow_Backend.Common;

namespace NestFlow_Backend.Middlewares;

/// <summary>
/// 將 <see cref="AppException"/> 轉為對應狀態碼，其餘例外一律回傳 500 且不外洩細節。
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未預期的錯誤：{Path}", context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "系統發生錯誤，請稍後再試。");
        }
    }

    private static Task WriteAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
    }
}
