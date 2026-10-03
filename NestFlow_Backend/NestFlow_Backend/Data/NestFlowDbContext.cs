using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data;

/// <summary>
/// NestFlow 資料庫內容。
/// 實體與資料表對應由 Data/Configurations 下的 IEntityTypeConfiguration 定義，
/// 一律將 PascalCase 實體對應為 snake_case 資料表與欄位。
/// </summary>
public class NestFlowDbContext : DbContext
{
    public NestFlowDbContext(DbContextOptions<NestFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();

    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

    public DbSet<AccountEntry> AccountEntries => Set<AccountEntry>();

    public DbSet<AccountEntryShare> AccountEntryShares => Set<AccountEntryShare>();

    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    public DbSet<Todo> Todos => Set<Todo>();

    public DbSet<StorageItem> StorageItems => Set<StorageItem>();

    public DbSet<PendingAction> PendingActions => Set<PendingAction>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    public DbSet<BindingCode> BindingCodes => Set<BindingCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NestFlowDbContext).Assembly);

        ApplyMySqlWorkarounds(modelBuilder);
        ApplySqliteDateTimeOffsetWorkaround(modelBuilder);
    }

    private void ApplyMySqlWorkarounds(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        // MariaDB 沒有 SQL Server 的 filtered index；保留唯一索引，由應用程式
        // 及資料表狀態規則維持提醒的有效性。
        modelBuilder.Entity<Reminder>()
            .HasIndex(x => x.CalendarEventId)
            .HasFilter(null);

        // MySQL/MariaDB 沒有帶 offset 的 datetime。所有時間一律以 UTC 寫入，
        // 讀取時再標示為 UTC，與既有服務的時間規則一致。
        var converter = new ValueConverter<DateTimeOffset, DateTime>(
            value => value.UtcDateTime,
            value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));
        var nullableConverter = new ValueConverter<DateTimeOffset?, DateTime?>(
            value => value == null ? null : value.Value.UtcDateTime,
            value => value == null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(converter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableConverter);
                }
            }
        }
    }

    /// <summary>
    /// SQLite 無法直接比較或排序 DateTimeOffset，測試以 SQLite 執行時改存 UTC Ticks。
    /// 正式使用的 SQL Server 不受影響。
    /// </summary>
    private void ApplySqliteDateTimeOffsetWorkaround(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        var converter = new ValueConverter<DateTimeOffset, long>(
            value => value.UtcDateTime.Ticks,
            value => new DateTimeOffset(value, TimeSpan.Zero));

        var nullableConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value == null ? null : value.Value.UtcDateTime.Ticks,
            value => value == null ? null : new DateTimeOffset(value.Value, TimeSpan.Zero));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(converter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableConverter);
                }
            }
        }
    }
}
