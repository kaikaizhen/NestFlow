using NestFlow_Backend.Models.Dtos;

namespace NestFlow_Backend.Services;

public interface IReminderService
{
    Task<ReminderDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveReminderCommand command,
        CancellationToken cancellationToken);

    /// <summary>取消提醒。已發送的提醒不可取消。</summary>
    Task CancelAsync(Guid userId, Guid reminderId, CancellationToken cancellationToken);

    Task<List<ReminderDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken);
}
