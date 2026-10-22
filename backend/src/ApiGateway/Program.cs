using System.Text.Json.Serialization;
using ApiGateway.Gateway;
using ApiGateway.Infrastructure.Auth;
using ApiGateway.Infrastructure.Configuration;
using ApiGateway.Infrastructure.Data;
using ApiGateway.Middleware;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Models.Responses;
using ApiGateway.Repositories.Implementations;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Yarp.ReverseProxy.Configuration;

const string CorsPolicyName = "FrontendClient";
const string ConnectionStringName = "GatewayDb";
const string HealthCheckPath = "/healthz";

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 帳號密碼（資料庫、JWT 密鑰、初始管理者）由 backend/.env 提供，不寫在 appsettings
    builder.Configuration.AddDotEnv(builder.Environment.ContentRootPath);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.WebHost.ConfigureKestrel(options =>
    {
        // 憲章八：移除 Server 標頭；憲章七：全域請求大小上限
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = GatewayLimits.MaxRequestBodySizeBytes;
    });

    // ---- MVC / JSON ----
    builder.Services.AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        // 模型繫結錯誤（含 JsonRequired 缺欄位）統一為 { success:false, code, message, details }
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            return new BadRequestObjectResult(ApiResponse.Fail(ErrorCodes.ValidationFailed, "Validation failed", errors));
        };
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ---- CORS ----
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Contains("*"))
    {
        throw new InvalidOperationException("Cors:AllowedOrigins must not contain '*' when credentials are allowed.");
    }

    builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Content-Disposition")));

    // ---- 基礎元件 ----
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHealthChecks();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
    builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

    builder.Services.AddDbContext<GatewayDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString(ConnectionStringName)));

    builder.Services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
    builder.Services.AddSingleton<ITokenService, TokenService>();
    builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

    // ---- Repositories ----
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
    builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
    builder.Services.AddScoped<IConfigDraftRepository, ConfigDraftRepository>();
    builder.Services.AddScoped<IConfigVersionRepository, ConfigVersionRepository>();

    // ---- Services ----
    builder.Services.AddScoped<IAccessControlService, AccessControlService>();
    builder.Services.AddScoped<IAuditTrailService, AuditTrailService>();
    // F-15 帳號管理：登入、登出、目前使用者
    builder.Services.AddScoped<IAuthService, AuthService>();
    // F-13 參數版本控制：草稿狀態
    builder.Services.AddScoped<IDraftService, DraftService>();

    // 主機群組（範例 CRUD 模組）
    builder.Services.AddScoped<IClusterRepository, ClusterRepository>();
    builder.Services.AddScoped<IClusterService, ClusterService>();

    // ---- Gateway（YARP 動態設定）----
    builder.Services.AddSingleton<DatabaseProxyConfigProvider>();
    builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DatabaseProxyConfigProvider>());
    builder.Services.AddReverseProxy();

    // 背景工作：依註冊順序啟動（先初始化資料庫，再載入生效版本）
    builder.Services.AddHostedService<DatabaseInitializer>();
    builder.Services.AddHostedService<ProxyConfigLoader>();

    var app = builder.Build();

    // ---- Middleware 管線（順序不可任意調整，見 Architecture.md 3.2）----
    // index.html 不可被瀏覽器快取，否則重新打包後仍載入舊版 JS（assets/ 檔名含 hash，可正常快取）
    var spaFileOptions = new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                context.Context.Response.Headers.CacheControl = "no-cache";
            }
        },
    };
    app.UseDefaultFiles();
    app.UseStaticFiles(spaFileOptions);

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors(CorsPolicyName);

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey("Cache-Control"))
                {
                    context.Response.Headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });
        }

        await next();
    });

    app.UseMiddleware<RequestResponseLoggingMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<AuthMiddleware>();

    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks(HealthCheckPath);
    app.MapReverseProxy();

    // 未知的 /api 路徑回 404，不交給 SPA fallback
    app.Map("/api/{**path}", (HttpContext context) =>
        Results.Json(ApiResponse.Fail(ErrorCodes.NotFound, "API not found"), statusCode: StatusCodes.Status404NotFound));
    app.MapFallbackToFile("index.html", spaFileOptions);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "應用程式啟動失敗");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>供整合測試（WebApplicationFactory）參照</summary>
public partial class Program;
