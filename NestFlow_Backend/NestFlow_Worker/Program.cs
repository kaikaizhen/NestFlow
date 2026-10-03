using Microsoft.EntityFrameworkCore;
using NestFlow_Backend.Common;
using NestFlow_Backend.Data;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Repositories;
using NestFlow_Backend.Services;
using NestFlow_Backend.Services.External;
using NestFlow_Worker;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------
// 設定綁定。機密值一律由環境變數注入，與 API 使用相同的鍵名。
// ---------------------------------------------------------------
builder.Services
    .AddOptions<EncryptionOptions>()
    .Bind(builder.Configuration.GetSection(EncryptionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<LineMessagingOptions>(
    builder.Configuration.GetSection(LineMessagingOptions.SectionName));

// ---------------------------------------------------------------
// 資料存取與提醒派送所需的服務
// ---------------------------------------------------------------
builder.Services.AddDbContext<NestFlowDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("NestFlowDb"),
        new MariaDbServerVersion(new Version(10, 11, 8)),
        mysql => mysql.EnableRetryOnFailure()));

builder.Services.AddSingleton<ICryptoHelper, CryptoHelper>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IReminderRepository, ReminderRepository>();
builder.Services.AddScoped<IReminderDispatchService, ReminderDispatchService>();

builder.Services.AddHttpClient<ILineMessagingClient, LineMessagingClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHostedService<ReminderWorker>();

var host = builder.Build();
host.Run();
