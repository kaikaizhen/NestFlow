using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public class TodoRepository : ITodoRepository
{
    private readonly NestFlowDbContext _dbContext;

    public TodoRepository(NestFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Todo todo, CancellationToken cancellationToken)
    {
        await _dbContext.Todos.AddAsync(todo, cancellationToken);
    }

    public Task<Todo?> GetActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Todos.FirstOrDefaultAsync(
            x => x.Id == id && x.Status == TodoStatus.Active,
            cancellationToken);
    }

    public async Task<List<Todo>> ListAsync(
        Guid workspaceId,
        TodoType type,
        bool? completed,
        int limit,
        CancellationToken cancellationToken)
    {
        if (completed == false)
        {
            return await PendingQuery(workspaceId, type).Take(limit).ToListAsync(cancellationToken);
        }

        if (completed == true)
        {
            return await CompletedQuery(workspaceId, type).Take(limit).ToListAsync(cancellationToken);
        }

        // 未完成先取滿，已完成只補剩餘額度，
        // 確保待辦事項不會被累積的歷史紀錄擠出查詢上限。
        var pending = await PendingQuery(workspaceId, type).Take(limit).ToListAsync(cancellationToken);
        var remaining = limit - pending.Count;

        if (remaining <= 0)
        {
            return pending;
        }

        var done = await CompletedQuery(workspaceId, type).Take(remaining).ToListAsync(cancellationToken);

        return [.. pending, .. done];
    }

    /// <summary>未完成：有到期時間的依時間由近到遠排在前面，未設到期時間的排在後面。</summary>
    private IQueryable<Todo> PendingQuery(Guid workspaceId, TodoType type)
    {
        return BaseQuery(workspaceId, type)
            .Where(x => x.CompletedAt == null)
            .OrderBy(x => x.DueAt == null)
            .ThenBy(x => x.DueAt)
            .ThenBy(x => x.CreatedAt);
    }

    /// <summary>已完成：最近完成的排在前面。</summary>
    private IQueryable<Todo> CompletedQuery(Guid workspaceId, TodoType type)
    {
        return BaseQuery(workspaceId, type)
            .Where(x => x.CompletedAt != null)
            .OrderByDescending(x => x.CompletedAt);
    }

    /// <summary>共用篩選：只取指定 Workspace 與類型中未刪除的代辦。</summary>
    private IQueryable<Todo> BaseQuery(Guid workspaceId, TodoType type)
    {
        return _dbContext.Todos
            .Include(x => x.User)
            .Where(x =>
                x.WorkspaceId == workspaceId
                && x.Status == TodoStatus.Active
                && x.Type == type);
    }
}
