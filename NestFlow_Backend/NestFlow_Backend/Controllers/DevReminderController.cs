using Microsoft.AspNetCore.Mvc;
using NestFlow_Backend.Services;

namespace NestFlow_Backend.Controllers;

/// <summary>
/// 開發專用的提醒派送觸發端點。
/// 正式環境由 Background Worker 定期執行，這裡讓開發與測試不必等排程。
/// 僅在 Development 與 Testing 環境生效。
/// </summary>
[ApiController]
[Route("api/dev/reminders")]
public class DevReminderController : ControllerBase
{
    private readonly IReminderDispatchService _dispatchService;
    private readonly IWebHostEnvironment _environment;

    public DevReminderController(
        IReminderDispatchService dispatchService,
        IWebHostEnvironment environment)
    {
        _dispatchService = dispatchService;
        _environment = environment;
    }

    [HttpPost("dispatch")]
    public async Task<ActionResult<ReminderDispatchResult>> Dispatch(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        return Ok(await _dispatchService.DispatchDueAsync(cancellationToken));
    }
}
