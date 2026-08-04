using NestFlow_Backend.Common;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.Entities;
using NestFlow_Backend.Repositories;

namespace NestFlow_Backend.Services;

public class TodoService : ITodoService
{
    /// <summary>單次查詢的最大筆數，避免一次拉回過量資料。</summary>
    private const int MaxLimit = 500;

    private readonly ITodoRepository _todoRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public TodoService(
        ITodoRepository todoRepository,
        IWorkspaceService workspaceService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _todoRepository = todoRepository;
        _workspaceService = workspaceService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<TodoDtoModel> GetAsync(Guid userId, Guid todoId, CancellationToken cancellationToken)
    {
        var todo = await GetAccessibleTodoAsync(userId, todoId, cancellationToken);
        var owner = await _userRepository.GetByIdAsync(todo.UserId, cancellationToken);

        return ToDto(todo, owner?.DisplayName ?? string.Empty);
    }

    public async Task<TodoDtoModel> CreateAsync(
        Guid userId,
        Guid workspaceId,
        SaveTodoCommand command,
        CancellationToken cancellationToken)
    {
        // 前端傳入的 workspaceId 一律重新驗證 Membership
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);

        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            Type = command.Type,
            Title = command.Title,
            Quantity = NormalizeQuantity(command),
            DueAt = command.DueAtUtc,
            Status = TodoStatus.Active,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        await _todoRepository.AddAsync(todo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return ToDto(todo, user?.DisplayName ?? string.Empty);
    }

    public async Task<TodoDtoModel> UpdateAsync(
        Guid userId,
        Guid todoId,
        Guid workspaceId,
        SaveTodoCommand command,
        CancellationToken cancellationToken)
    {
        var todo = await GetAccessibleTodoAsync(userId, todoId, cancellationToken);

        // 與記帳一致，第一版不支援把代辦搬到其他資料空間
        if (todo.WorkspaceId != workspaceId)
        {
            throw AppException.BadRequest("不能變更代辦所屬的資料空間。");
        }

        todo.Type = command.Type;
        todo.Title = command.Title;
        todo.Quantity = NormalizeQuantity(command);
        todo.DueAt = command.DueAtUtc;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var owner = await _userRepository.GetByIdAsync(todo.UserId, cancellationToken);
        return ToDto(todo, owner?.DisplayName ?? string.Empty);
    }

    public async Task<TodoDtoModel> SetCompletionAsync(
        Guid userId,
        Guid todoId,
        bool completed,
        CancellationToken cancellationToken)
    {
        var todo = await GetAccessibleTodoAsync(userId, todoId, cancellationToken);

        if (completed)
        {
            // 已完成的再標記一次時保留原本的完成時間，避免已完成列表順序無故跳動
            todo.CompletedAt ??= _timeProvider.GetUtcNow();
        }
        else
        {
            todo.CompletedAt = null;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var owner = await _userRepository.GetByIdAsync(todo.UserId, cancellationToken);
        return ToDto(todo, owner?.DisplayName ?? string.Empty);
    }

    public async Task DeleteAsync(Guid userId, Guid todoId, CancellationToken cancellationToken)
    {
        var todo = await GetAccessibleTodoAsync(userId, todoId, cancellationToken);

        todo.Status = TodoStatus.Deleted;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TodoDtoModel>> ListAsync(
        Guid userId,
        Guid workspaceId,
        TodoType type,
        bool? completed,
        int? limit,
        CancellationToken cancellationToken)
    {
        await _workspaceService.EnsureMemberAsync(userId, workspaceId, cancellationToken);

        var todos = await _todoRepository.ListAsync(
            workspaceId,
            type,
            completed,
            Math.Clamp(limit ?? MaxLimit, 1, MaxLimit),
            cancellationToken);

        return todos
            .Select(x => ToDto(x, x.User?.DisplayName ?? string.Empty))
            .ToList();
    }

    /// <summary>一般代辦不保存數量，避免從購物清單改為一般代辦後殘留上一個類型的欄位。</summary>
    private static int? NormalizeQuantity(SaveTodoCommand command)
    {
        return command.Type == TodoType.Shopping ? command.Quantity : null;
    }

    /// <summary>
    /// 取得代辦並確認呼叫者是該 Workspace 的有效成員。
    /// 家庭空間內任何成員都可修改、完成與刪除共同代辦，符合計畫第 7.4 節。
    /// </summary>
    private async Task<Todo> GetAccessibleTodoAsync(
        Guid userId,
        Guid todoId,
        CancellationToken cancellationToken)
    {
        var todo = await _todoRepository.GetActiveAsync(todoId, cancellationToken)
            ?? throw AppException.NotFound();

        await _workspaceService.EnsureMemberAsync(userId, todo.WorkspaceId, cancellationToken);

        return todo;
    }

    private static TodoDtoModel ToDto(Todo todo, string displayName) => new()
    {
        Id = todo.Id,
        Type = todo.Type,
        Title = todo.Title,
        Quantity = todo.Quantity,
        DueAt = todo.DueAt,
        CompletedAt = todo.CompletedAt,
        CreatedByUserId = todo.UserId,
        CreatedByDisplayName = displayName,
    };
}
