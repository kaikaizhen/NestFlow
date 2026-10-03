using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Data;
using NestFlow_Backend.Models.Entities;

var sourceConnection = GetArgument(args, "--source")
    ?? Environment.GetEnvironmentVariable("NESTFLOW_SOURCE_CONNECTION");
var targetConnection = GetArgument(args, "--target")
    ?? Environment.GetEnvironmentVariable("NESTFLOW_TARGET_CONNECTION");

if (string.IsNullOrWhiteSpace(sourceConnection) || string.IsNullOrWhiteSpace(targetConnection))
{
    Console.Error.WriteLine("Usage: --source <SQL Server connection string> --target <MySQL connection string>");
    return 2;
}

var sourceOptions = new DbContextOptionsBuilder<NestFlowDbContext>()
    .UseSqlServer(sourceConnection)
    .Options;
var targetOptions = new DbContextOptionsBuilder<NestFlowDbContext>()
    .UseMySql(targetConnection, new MariaDbServerVersion(new Version(10, 11, 8)))
    .Options;

await using var source = new NestFlowDbContext(sourceOptions);
await using var target = new NestFlowDbContext(targetOptions);

if (!await source.Database.CanConnectAsync())
{
    throw new InvalidOperationException("無法連線至來源 SQL Server。");
}

await target.Database.EnsureCreatedAsync();

if (await HasExistingDataAsync(target))
{
    throw new InvalidOperationException("目標 MySQL 已有 NestFlow 資料；為避免覆寫或重複匯入，已停止移植。");
}

Console.WriteLine("開始移植 NestFlow 資料…");
await CopyAsync(source.Users, target.Users, target, "users");
await CopyAsync(source.Workspaces, target.Workspaces, target, "workspaces");
await CopyAsync(source.ExternalIdentities, target.ExternalIdentities, target, "external_identities");
await CopyAsync(source.Sessions, target.Sessions, target, "sessions");
await CopyAsync(source.WorkspaceMemberships, target.WorkspaceMemberships, target, "workspace_memberships");
await CopyAsync(source.WorkspaceInvitations, target.WorkspaceInvitations, target, "workspace_invitations");
await CopyAsync(source.AccountEntries, target.AccountEntries, target, "account_entries");
await CopyAsync(source.AccountEntryShares, target.AccountEntryShares, target, "account_entry_shares");
await CopyAsync(source.CalendarEvents, target.CalendarEvents, target, "calendar_events");
await CopyAsync(source.Reminders, target.Reminders, target, "reminders");
await CopyAsync(source.Todos, target.Todos, target, "todos");
await CopyAsync(source.StorageItems, target.StorageItems, target, "storage_items");
await CopyAsync(source.PendingActions, target.PendingActions, target, "pending_actions");
await CopyAsync(source.ProcessedEvents, target.ProcessedEvents, target, "processed_events");
await CopyAsync(source.BindingCodes, target.BindingCodes, target, "binding_codes");

Console.WriteLine("移植完成。");
return 0;

static string? GetArgument(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static async Task<bool> HasExistingDataAsync(NestFlowDbContext context) =>
    await context.Users.AnyAsync()
    || await context.Workspaces.AnyAsync()
    || await context.AccountEntries.AnyAsync()
    || await context.CalendarEvents.AnyAsync()
    || await context.Todos.AnyAsync()
    || await context.StorageItems.AnyAsync();

static async Task CopyAsync<TEntity>(
    DbSet<TEntity> source,
    DbSet<TEntity> target,
    NestFlowDbContext targetContext,
    string name)
    where TEntity : class
{
    var rows = await source.AsNoTracking().ToListAsync();
    if (rows.Count == 0)
    {
        Console.WriteLine($"{name}: 0");
        return;
    }

    await target.AddRangeAsync(rows);
    await targetContext.SaveChangesAsync();
    targetContext.ChangeTracker.Clear();
    Console.WriteLine($"{name}: {rows.Count}");
}
