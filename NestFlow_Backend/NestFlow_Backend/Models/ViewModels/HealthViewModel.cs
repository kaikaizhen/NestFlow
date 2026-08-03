namespace NestFlow_Backend.Models.ViewModels;

/// <summary>
/// 服務基本資訊回傳格式。
/// </summary>
public class HealthViewModel
{
    public string Service { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public DateTimeOffset ServerTime { get; set; }
}
