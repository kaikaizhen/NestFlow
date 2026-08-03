using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class ReminderRepository : IReminderRepository
{
    private readonly NestFlowDbContext _dbContext;

    public ReminderRepository(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        await _dbContext.Reminders.AddAsync(reminder, cancellationToken);
    }

    public Task<Reminder?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Reminders.FirstOrDefaultAsync(
            x => x.Id == id && x.Status != ReminderStatus.Cancelled,
            cancellationToken);
    }

    public Task<List<Reminder>> ListAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Reminders
            .Where(x => x.WorkspaceId == workspaceId
                && x.Status != ReminderStatus.Cancelled
                && x.TriggerAt >= fromUtc
                && x.TriggerAt < toUtc)
            .Include(x => x.User)
            .OrderBy(x => x.TriggerAt)
            .ThenBy(x => x.CreatedAt);

        return limit is > 0
            ? query.Take(limit.Value).ToListAsync(cancellationToken)
            : query.ToListAsync(cancellationToken);
    }

    public async Task<List<Reminder>> ClaimDueAsync(
        DateTimeOffset nowUtc,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var claimed = new List<Reminder>();

        var candidates = await _dbContext.Reminders
            .Where(x => x.Status == ReminderStatus.Pending && x.TriggerAt <= nowUtc)
            .OrderBy(x => x.TriggerAt)
            .Take(maxCount)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in candidates)
        {
            // 條件式更新：Status 仍是 Pending 才會成功，
            // 兩個 Worker 同時掃到同一筆時只有一邊的 affected 會是 1。
            var affected = await _dbContext.Reminders
                .Where(x => x.Id == id && x.Status == ReminderStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.Status, ReminderStatus.Sending),
                    cancellationToken);

            if (affected != 1)
            {
                continue;
            }

            var reminder = await _dbContext.Reminders
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (reminder is not null)
            {
                claimed.Add(reminder);
            }
        }

        return claimed;
    }
}
