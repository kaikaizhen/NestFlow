using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Dtos;

public class ReminderDtoModel
{
    public Guid Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset TriggerAt { get; set; }

    public ReminderStatus Status { get; set; }

    public int RetryCount { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;
}

/// <summary>提醒寫入用的命令，欄位皆已通過驗證與正規化。</summary>
public record SaveReminderCommand(string Content, DateTimeOffset TriggerAtUtc);
