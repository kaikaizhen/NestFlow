using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

/// <summary>
/// 已處理過的外部事件。LINE 會重送 Webhook，靠此表確保同一事件只處理一次。
/// </summary>
public class ProcessedEvent
{
    public Guid Id { get; set; }

    public IdentityProvider Provider { get; set; }

    public string ExternalEventId { get; set; } = string.Empty;

    public ProcessedEventStatus Status { get; set; } = ProcessedEventStatus.Processed;

    public DateTimeOffset CreatedAt { get; set; }
}
