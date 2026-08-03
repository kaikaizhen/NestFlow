using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class MessagingRepository : IMessagingRepository
{
    private readonly NestFlowDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public MessagingRepository(NestFlowDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<bool> TryMarkEventProcessedAsync(
        IdentityProvider provider,
        string externalEventId,
        CancellationToken cancellationToken)
    {
        _dbContext.ProcessedEvents.Add(new ProcessedEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ExternalEventId = externalEventId,
            Status = ProcessedEventStatus.Processed,
            CreatedAt = _timeProvider.GetUtcNow(),
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // 唯一索引擋下重複事件，代表已處理過。清掉追蹤狀態讓後續操作可繼續使用同一個 DbContext。
            _dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task AddPendingActionAsync(PendingAction action, CancellationToken cancellationToken)
    {
        await _dbContext.PendingActions.AddAsync(action, cancellationToken);
    }

    public async Task<PendingAction?> GetPendingActionAsync(
        Guid userId,
        IdentityProvider provider,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var action = await _dbContext.PendingActions
            .Where(x =>
                x.UserId == userId
                && x.Provider == provider
                && x.Status == PendingActionStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (action is null)
        {
            return null;
        }

        if (action.ExpiresAt <= now)
        {
            action.Status = PendingActionStatus.Expired;
            return null;
        }

        return action;
    }

    public async Task<int> SupersedePendingActionsAsync(
        Guid userId,
        IdentityProvider provider,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.PendingActions
            .Where(x =>
                x.UserId == userId
                && x.Provider == provider
                && x.Status == PendingActionStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var action in existing)
        {
            action.Status = PendingActionStatus.Superseded;
        }

        return existing.Count;
    }

    public async Task AddBindingCodeAsync(BindingCode code, CancellationToken cancellationToken)
    {
        await _dbContext.BindingCodes.AddAsync(code, cancellationToken);
    }

    public Task<BindingCode?> GetBindingCodeByHashAsync(string codeHash, CancellationToken cancellationToken)
    {
        return _dbContext.BindingCodes.FirstOrDefaultAsync(x => x.CodeHash == codeHash, cancellationToken);
    }
}
