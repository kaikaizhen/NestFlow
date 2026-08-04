using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Repositories;

public interface ITodoRepository
{
    Task AddAsync(Todo todo, CancellationToken cancellationToken);

    /// <summary>取得未刪除的單筆代辦。</summary>
    Task<Todo?> GetActiveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 依 Workspace 與類型取得未刪除的代辦。
    /// <paramref name="completed"/> 為 null 時兩者都取，未完成一律排在已完成之前。
    /// </summary>
    Task<List<Todo>> ListAsync(
        Guid workspaceId,
        TodoType type,
        bool? completed,
        int limit,
        CancellationToken cancellationToken);
}
