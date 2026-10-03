using Microsoft.AspNetCore.Mvc.Filters;
using NestFlow_Backend.Common;

namespace NestFlow_Backend.Filters;

/// <summary>
/// 要求請求必須帶有有效 Session。套用於需要登入的 Controller 或 Action。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireSessionAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var accessor = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserAccessor>();

        if (accessor.UserId is null)
        {
            // 交由 Library 的全域例外處理輸出 RFC 9457 Problem Details，
            // 避免授權失敗與商業規則錯誤有不同的回應格式。
            throw AppException.Unauthorized();
        }
    }
}
