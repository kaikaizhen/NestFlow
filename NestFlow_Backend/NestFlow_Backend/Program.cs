using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Library.Logging;
using Library.Observability;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Middlewares;
using NestFlow_Backend.Repositories;
using NestFlow_Backend.Services;
using NestFlow_Backend.Services.External;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 統一由 Library 建立 OpenTelemetry 與 Serilog 管線。未設定 OTLP endpoint
// 時仍保留本機診斷與 console log；日後接入 Alloy/Grafana 時只需提供設定。
builder.AddLibraryObservability();
builder.AddLibrarySerilog();

// ---------------------------------------------------------------
// 設定綁定（機密值只存在 appsettings.Development.json 或環境變數）
// ---------------------------------------------------------------
builder.Services
    .AddOptions<EncryptionOptions>()
    .Bind(builder.Configuration.GetSection(EncryptionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<LineLoginOptions>(
    builder.Configuration.GetSection(LineLoginOptions.SectionName));

builder.Services.Configure<LineMessagingOptions>(
    builder.Configuration.GetSection(LineMessagingOptions.SectionName));

builder.Services.Configure<DifyOptions>(
    builder.Configuration.GetSection(DifyOptions.SectionName));

builder.Services.Configure<NestFlow_Backend.Common.SessionOptions>(
    builder.Configuration.GetSection(NestFlow_Backend.Common.SessionOptions.SectionName));

// ---------------------------------------------------------------
// 資料存取
// ---------------------------------------------------------------
builder.Services.AddDbContext<NestFlowDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("NestFlowDb"),
        new MariaDbServerVersion(new Version(10, 11, 8)),
        mysql => mysql.EnableRetryOnFailure()));

// ---------------------------------------------------------------
// 共用服務
// ---------------------------------------------------------------
builder.Services.AddSingleton<ICryptoHelper, CryptoHelper>();
builder.Services.AddSingleton<ICodeGenerator, CodeGenerator>();
builder.Services.AddSingleton<IFixedFormatParser, FixedFormatParser>();
builder.Services.AddSingleton<ILineSignatureValidator, LineSignatureValidator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Repository 層
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
builder.Services.AddScoped<IAccountEntryRepository, AccountEntryRepository>();
builder.Services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
builder.Services.AddScoped<IReminderRepository, ReminderRepository>();
builder.Services.AddScoped<ITodoRepository, TodoRepository>();
builder.Services.AddScoped<IStorageItemRepository, StorageItemRepository>();
builder.Services.AddScoped<IMessagingRepository, MessagingRepository>();

// Service 層
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountEntryService, AccountEntryService>();
builder.Services.AddScoped<ICalendarEventService, CalendarEventService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IReminderDispatchService, ReminderDispatchService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<IStorageItemService, StorageItemService>();
builder.Services.AddScoped<ILineWebhookService, LineWebhookService>();

builder.Services.AddHttpClient<ILineMessagingClient, LineMessagingClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHttpClient<IDifyClient, DifyClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));

// LINE Login 對外呼叫與 JWKS 快取
builder.Services.AddHttpClient<ILineLoginClient, LineLoginClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LineLoginOptions>>().Value;

    return new ConfigurationManager<OpenIdConnectConfiguration>(
        options.MetadataAddress,
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever { RequireHttps = true });
});

// ---------------------------------------------------------------
// CORS：僅允許設定檔列出的 PWA 來源
// ---------------------------------------------------------------
const string PwaCorsPolicy = "PwaCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(PwaCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ---------------------------------------------------------------
// Health Check
// ---------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [GlobalConstants.LiveTag])
    .AddDbContextCheck<NestFlowDbContext>("database", tags: [GlobalConstants.ReadyTag]);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Configuration.GetValue<bool?>("Library:Logging:UseRequestLogging") ?? true)
{
    app.UseSerilogRequestLogging();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // 開發環境自動套用 Migration，讓資料表與程式碼保持同步。
    await MigrateDatabaseAsync(app);
}

// 部署在反向代理後面時（ASPNETCORE_FORWARDEDHEADERS_ENABLED=true），
// 讓 ASP.NET Core 依 X-Forwarded-* 標頭還原真實的 Scheme／IP，直接執行時預設關閉不影響行為。
if (builder.Configuration.GetValue("ASPNETCORE_FORWARDEDHEADERS_ENABLED", false))
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };

    // 反向代理跑在另一個容器，位址不固定，信任範圍交由外層網路（Docker network／NAS 防火牆）把關
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();

    app.UseForwardedHeaders(forwardedHeadersOptions);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors(PwaCorsPolicy);
app.UseMiddleware<SessionAuthenticationMiddleware>();
app.UseAuthorization();

app.MapHealthChecks(GlobalConstants.LivenessEndpoint, new()
{
    Predicate = check => check.Tags.Contains(GlobalConstants.LiveTag),
    ResponseWriter = WriteHealthResponseAsync
});

app.MapHealthChecks(GlobalConstants.ReadinessEndpoint, new()
{
    Predicate = check => check.Tags.Contains(GlobalConstants.ReadyTag),
    ResponseWriter = WriteHealthResponseAsync
});

app.MapControllers();

app.Run();

static async Task MigrateDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<NestFlowDbContext>();
        if (dbContext.Database.IsMySql())
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        else
        {
            await dbContext.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        // 資料庫尚未就緒不應阻擋 API 啟動，/health/ready 會如實回報 Unhealthy。
        logger.LogWarning(ex, "無法建立或連線資料庫，請確認 SQL Server 是否已啟動。");
    }
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    // 只輸出狀態名稱，不輸出例外訊息，避免連線字串等資訊外洩。
    var payload = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString()
        })
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

/// <summary>供整合測試以 WebApplicationFactory 啟動本組件。</summary>
public partial class Program;
