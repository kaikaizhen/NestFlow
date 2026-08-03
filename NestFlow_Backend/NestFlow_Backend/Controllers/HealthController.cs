using Microsoft.AspNetCore.Mvc;
using NestFlow_Backend.Models.ViewModels;

namespace NestFlow_Backend.Controllers;

/// <summary>
/// 服務資訊端點。存活與就緒探針另由 /health/live 與 /health/ready 提供。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public HealthController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    public ActionResult<HealthViewModel> Get()
    {
        return Ok(new HealthViewModel
        {
            Service = "NestFlow API",
            Status = "ok",
            Environment = _environment.EnvironmentName,
            ServerTime = DateTimeOffset.Now
        });
    }
}
