using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiGateway.Infrastructure.Data;

/// <summary>appsettings「Database」區段</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>啟動時自動套用 EF migration（僅建議 Development 開啟）</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>尚無任何帳號時建立的初始管理者帳號</summary>
    public string? SeedAdminUsername { get; set; }

    /// <summary>初始管理者密碼（機密，以環境變數 Database__SeedAdminPassword 注入）</summary>
    public string? SeedAdminPassword { get; set; }
}

/// <summary>
/// 啟動時套用 migration 並建立系統內建資料。資料庫無法連線時只記錄警告，
/// 不阻止 Gateway 啟動（憲章一：資料庫異常時持續以記憶體設定運作）。
/// </summary>
public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IServiceScopeFactory scopeFactory, IOptions<DatabaseOptions> options, ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

            if (_options.ApplyMigrationsOnStartup)
            {
                await db.Database.MigrateAsync(cancellationToken);
            }

            await SeedBuiltInPoliciesAsync(db, cancellationToken);
            await SeedAdminAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>(), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "資料庫初始化失敗，Gateway 仍會啟動");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedBuiltInPoliciesAsync(GatewayDbContext db, CancellationToken ct)
    {
        var authNames = await db.AuthPolicies.Where(x => x.IsBuiltIn).Select(x => x.PolicyName).ToListAsync(ct);
        if (!authNames.Contains(BuiltInPolicyNames.Default))
        {
            db.AuthPolicies.Add(new YarpAuthPolicy { PolicyName = BuiltInPolicyNames.Default, RequireAuthenticatedUser = true, IsBuiltIn = true });
        }

        if (!authNames.Contains(BuiltInPolicyNames.Anonymous))
        {
            db.AuthPolicies.Add(new YarpAuthPolicy { PolicyName = BuiltInPolicyNames.Anonymous, RequireAuthenticatedUser = false, IsBuiltIn = true });
        }

        var corsNames = await db.CorsPolicies.Where(x => x.IsBuiltIn).Select(x => x.PolicyName).ToListAsync(ct);
        if (!corsNames.Contains(BuiltInPolicyNames.Default))
        {
            db.CorsPolicies.Add(new YarpCorsPolicy { PolicyName = BuiltInPolicyNames.Default, IsBuiltIn = true });
        }

        if (!corsNames.Contains(BuiltInPolicyNames.Disable))
        {
            db.CorsPolicies.Add(new YarpCorsPolicy { PolicyName = BuiltInPolicyNames.Disable, IsBuiltIn = true });
        }
    }

    private async Task SeedAdminAsync(GatewayDbContext db, IPasswordHasher<AppUser> hasher, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SeedAdminUsername) || string.IsNullOrWhiteSpace(_options.SeedAdminPassword))
        {
            return;
        }

        if (await db.AppUsers.AnyAsync(ct))
        {
            return;
        }

        var admin = new AppUser
        {
            Username = _options.SeedAdminUsername,
            DisplayName = _options.SeedAdminUsername,
            Role = RoleCodes.Admin,
            IsActive = true,
            MustChangePassword = true,
        };
        admin.PasswordHash = hasher.HashPassword(admin, _options.SeedAdminPassword);
        db.AppUsers.Add(admin);

        _logger.LogWarning("Security: 已建立初始管理者帳號 {Username}，首次登入須變更密碼", admin.Username);
    }
}
