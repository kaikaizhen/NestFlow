using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class CalendarEventRepository : ICalendarEventRepository
{
    private readonly NestFlowDbContext _dbContext;

    public CalendarEventRepository(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        await _dbContext.CalendarEvents.AddAsync(calendarEvent, cancellationToken);
    }

    public Task<CalendarEvent?> GetActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CalendarEvents.FirstOrDefaultAsync(
            x => x.Id == id && x.Status == CalendarEventStatus.Active,
            cancellationToken);
    }

    public Task<List<CalendarEvent>> ListActiveBySeriesAsync(
        Guid recurrenceGroupId,
        CancellationToken cancellationToken)
    {
        return _dbContext.CalendarEvents
            .Where(x => x.RecurrenceGroupId == recurrenceGroupId && x.Status == CalendarEventStatus.Active)
            .OrderBy(x => x.StartAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<CalendarEvent>> ListAsync(
        Guid workspaceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        // 重疊判斷：行程開始早於區間結束，且結束晚於區間開始
        var query = _dbContext.CalendarEvents
            .Where(x => x.WorkspaceId == workspaceId
                && x.Status == CalendarEventStatus.Active
                && x.StartAt < toUtc
                && x.EndAt > fromUtc)
            .Include(x => x.User)
            .OrderBy(x => x.StartAt)
            .ThenBy(x => x.CreatedAt);

        return limit is > 0
            ? query.Take(limit.Value).ToListAsync(cancellationToken)
            : query.ToListAsync(cancellationToken);
    }
}
