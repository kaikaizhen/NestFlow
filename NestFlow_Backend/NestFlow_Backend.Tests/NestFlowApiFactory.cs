using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NestFlow_Backend.Data;

namespace NestFlow_Backend.Tests;

/// <summary>
/// 測試用的 API 主機。以測試專用金鑰覆寫設定，並將資料庫換成 SQLite In-Memory，
/// 不讀取也不影響開發者本機的資料庫與 appsettings.Development.json。
/// </summary>
public class NestFlowApiFactory : WebApplicationFactory<Program>
{
    /// <summary>測試專用金鑰，僅供單元測試使用，與正式環境無關。</summary>
    public const string TestEncryptionKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    /// <summary>測試用的 Messaging Channel Secret，供 Webhook 簽章驗證測試使用。</summary>
    public const string TestChannelSecret = "test-messaging-channel-secret";

    // 連線保持開啟，SQLite In-Memory 資料庫才不會在測試中途被釋放
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = TestEncryptionKey,
                ["ConnectionStrings:NestFlowDb"] = "DataSource=:memory:",
                ["Session:RequireHttps"] = "false",
                ["LineLogin:ChannelId"] = "test-channel",
                ["LineMessaging:ChannelId"] = "test-messaging-channel",
                ["LineMessaging:ChannelSecret"] = TestChannelSecret,
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<NestFlowDbContext>));
            services.Remove(descriptor);

            _connection.Open();
            services.AddDbContext<NestFlowDbContext>(options => options.UseSqlite(_connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<NestFlowDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
