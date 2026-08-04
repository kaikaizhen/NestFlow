using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Dtos;

public class TodoDtoModel
{
    public Guid Id { get; set; }

    public TodoType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public int? Quantity { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    /// <summary>null 代表未完成。</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;
}

/// <summary>代辦寫入用的命令，欄位皆已通過驗證與正規化。</summary>
public record SaveTodoCommand(
    TodoType Type,
    string Title,
    int? Quantity,
    DateTimeOffset? DueAtUtc);
