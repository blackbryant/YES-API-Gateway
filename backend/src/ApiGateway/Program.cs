using System.Text.Json.Serialization;
using ApiGateway.Gateway;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Infrastructure.Auth;
using ApiGateway.Infrastructure.Configuration;
using ApiGateway.Infrastructure.Data;
using ApiGateway.Infrastructure.Ids;
using ApiGateway.Middleware;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Models.Responses;
using ApiGateway.Repositories.Implementations;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

const string CorsPolicyName = "FrontendClient";
const string ConnectionStringName = "GatewayDb";
const string ReverseProxySectionName = "ReverseProxy";

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

    // 001-02-dynamic-routing-ab-test：發佈（驗證 → 版本 → 熱更新）、ULID
    builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection(GatewayOptions.SectionName));
    builder.Services.AddSingleton<ISystemIdGenerator, UlidGenerator>();
    builder.Services.AddScoped<IConfigSnapshotRepository, ConfigSnapshotRepository>();
    builder.Services.AddScoped<IPublishComposer, PublishComposer>();
    builder.Services.AddScoped<IPublishService, PublishService>();
    builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
    builder.Services.AddScoped<IServiceReadSupport, ServiceReadSupport>();
    builder.Services.AddScoped<IServiceService, ServiceService>();
    builder.Services.AddSingleton<ExperimentRuntimeCache>();
    builder.Services.AddSingleton<ExperimentMetricsCollector>();
    builder.Services.AddSingleton<ExperimentStateReader>();
    builder.Services.AddSingleton<IExperimentStateReader>(sp => sp.GetRequiredService<ExperimentStateReader>());
    builder.Services.AddSingleton<IExperimentGatewayControl>(sp => sp.GetRequiredService<ExperimentStateReader>());
    builder.Services.AddScoped<IExperimentService, ExperimentService>();
    builder.Services.AddScoped<IGatewayNodeRepository, GatewayNodeRepository>();
    builder.Services.AddScoped<IExperimentRuntimeRepository, ExperimentRuntimeRepository>();
    builder.Services.AddScoped<IGatewayNodeService, GatewayNodeService>();
    builder.Services.AddScoped<ISystemRouteService, SystemRouteService>();

    // ---- Gateway（YARP 動態設定）----
    builder.Services.AddSingleton<DatabaseProxyConfigProvider>();
    builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DatabaseProxyConfigProvider>());
    builder.Services.AddSingleton<IGatewayConfigApplier>(sp => sp.GetRequiredService<DatabaseProxyConfigProvider>());
    // 靜態代理路由（appsettings ReverseProxy 區段）與資料庫動態路由兩個 Provider 並存，由 YARP 合併
    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection(ReverseProxySectionName))
        // 憲章八：代理回應一律移除後端揭露伺服器資訊的標頭
        .AddTransforms(context =>
        {
            foreach (var header in GatewayLimits.HiddenResponseHeaders)
            {
                context.AddResponseHeaderRemove(header, ResponseCondition.Always);
            }
        });

    // 背景工作：依註冊順序啟動（先初始化資料庫，再載入生效版本）
    builder.Services.AddHostedService<DatabaseInitializer>();
    builder.Services.AddHostedService<ProxyConfigLoader>();
    builder.Services.AddHostedService<ConfigSyncService>();
    builder.Services.AddHostedService<AutoRollbackMonitor>();
    builder.Services.AddHostedService<ExperimentScheduleAuditService>();

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
        if (context.Request.Path.StartsWithSegments(AuthRules.AdminApiPrefix))
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
    // 系統端點（Admin API、健康檢查）以最優先的路由順序註冊，動態路由無法遮蔽（FR-001、FR-002）
    var gatewayOptions = app.Services.GetRequiredService<IOptions<GatewayOptions>>().Value;
    static void SystemEndpointOrder(EndpointBuilder endpoint) => ((RouteEndpointBuilder)endpoint).Order = RouteRules.SystemEndpointOrder;

    app.MapControllers().Add(SystemEndpointOrder);
    app.MapHealthChecks(gatewayOptions.HealthCheck.Path).Add(SystemEndpointOrder);
    // 代理管線：先決定 A/B 組別（可能改送其他群組），再走 YARP 預設的會話黏性、負載平衡與被動健康檢查
    app.MapReverseProxy(proxyPipeline =>
    {
        proxyPipeline.UseMiddleware<ExperimentRoutingMiddleware>();
        proxyPipeline.UseSessionAffinity();
        proxyPipeline.UseLoadBalancing();
        proxyPipeline.UsePassiveHealthChecks();
    });

    // 未知的 Admin API 路徑回 404，不交給動態路由或 SPA fallback
    app.Map(AuthRules.AdminApiPrefix + "/{**path}", (HttpContext context) =>
        Results.Json(ApiResponse.Fail(ErrorCodes.NotFound, "API not found"), statusCode: StatusCodes.Status404NotFound))
        .Add(SystemEndpointOrder);
    // 管理介面只在 /admin 底下（前端路由交給 index.html）；其餘未命中的路徑回 404，不再回管理介面頁面
    app.MapFallbackToFile(AuthRules.AdminUiPrefix + "/{*path:nonfile}", AuthRules.AdminUiIndexFile, spaFileOptions).Add(SystemEndpointOrder);
    // 根路徑導向管理介面；以 fallback 註冊，動態路由若有對應 / 的入口仍以入口為準
    app.MapFallback("/", () => Results.Redirect(AuthRules.AdminUiPrefix + "/"));

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
