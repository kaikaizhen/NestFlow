using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NestFlow_Backend.Tests;

/// <summary>
/// 測試用的 API 主機。以測試專用金鑰與連線字串覆寫設定，
/// 不讀取開發者本機的 appsettings.Development.json。
/// </summary>
public class NestFlowApiFactory : WebApplicationFactory<Program>
{
    /// <summary>測試專用金鑰，僅供單元測試使用，與正式環境無關。</summary>
    public const string TestEncryptionKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = TestEncryptionKey,
                ["ConnectionStrings:NestFlowDb"] = "Server=(localdb)\\NestFlowTests;Database=NestFlowTests;Trusted_Connection=True"
            });
        });
    }
}
