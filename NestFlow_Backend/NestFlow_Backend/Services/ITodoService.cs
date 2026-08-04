using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Dtos;

namespace NestFlow_Backend.Services;

public interface ITodoService
{
    /// <summary>取得單筆代辦，供編輯畫面使用。</summary>
    Task<TodoDtoModel> GetAsync(Guid userId, Guid todoId, CancellationToken cancellationToken);

    Task<TodoDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveTodoCommand command,
        CancellationToken cancellationToken);

    Task<TodoDtoModel> UpdateAsync(
        Guid userId,
        Guid todoId,
        Guid workspaceId,
        SaveTodoCommand command,
        CancellationToken cancellationToken);

    /// <summary>標記完成或取消完成。</summary>
    Task<TodoDtoModel> SetCompletionAsync(
        Guid userId,
        Guid todoId,
        bool completed,
        CancellationToken cancellationToken);

    /// <summary>軟刪除，資料保留於資料庫但不再出現於列表。</summary>
    Task DeleteAsync(Guid userId, Guid todoId, CancellationToken cancellationToken);

    Task<List<TodoDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        TodoType type,
        bool? completed,
        int? limit,
        CancellationToken cancellationToken);
}
