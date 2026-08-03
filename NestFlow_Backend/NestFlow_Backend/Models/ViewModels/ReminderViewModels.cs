namespace NestFlow_Backend.Models.ViewModels;

public class ReminderViewModel
{
    public Guid Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset TriggerAt { get; set; }

    /// <summary>pending／sending／sent／failed。已取消的提醒不會出現在列表。</summary>
    public string Status { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;
}
