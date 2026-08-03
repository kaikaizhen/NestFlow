using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NestFlowDbContext).Assembly);
    }
}
