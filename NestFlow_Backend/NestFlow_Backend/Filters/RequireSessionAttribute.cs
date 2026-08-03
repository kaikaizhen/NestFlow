using Microsoft.AspNetCore.Mvc;
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
            context.Result = new ObjectResult(new { message = "尚未登入。" })
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };
        }
    }
}
