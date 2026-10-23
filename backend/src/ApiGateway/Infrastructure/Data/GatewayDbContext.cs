using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Infrastructure.Data;

/// <summary>
/// EF Core 資料庫內容。表格與欄位對應見 yes-specs/yarp_schema_v2.md；
/// Entity 不建立導覽屬性（憲章：Entities 不得有非資料庫欄位），關聯一律以 Fluent API 宣告外鍵。
/// </summary>
public class GatewayDbContext : DbContext
{
    private readonly TimeProvider _timeProvider;

    public GatewayDbContext(DbContextOptions<GatewayDbContext> options, TimeProvider timeProvider)
        : base(options)
    {
        _timeProvider = timeProvider;
    }

    // 核心資源
    public DbSet<YarpDestination> Destinations => Set<YarpDestination>();
    public DbSet<YarpDestinationHealth> DestinationHealth => Set<YarpDestinationHealth>();
    public DbSet<YarpCluster> Clusters => Set<YarpCluster>();
    public DbSet<YarpClusterHealthCheck> ClusterHealthChecks => Set<YarpClusterHealthCheck>();
    public DbSet<YarpClusterSessionAffinity> ClusterSessionAffinity => Set<YarpClusterSessionAffinity>();
    public DbSet<YarpService> Services => Set<YarpService>();
    public DbSet<YarpRoute> Routes => Set<YarpRoute>();
    public DbSet<YarpRouteMatchCondition> RouteMatchConditions => Set<YarpRouteMatchCondition>();
    public DbSet<YarpRouteTransform> RouteTransforms => Set<YarpRouteTransform>();

    // 策略
    public DbSet<YarpRateLimitPolicy> RateLimitPolicies => Set<YarpRateLimitPolicy>();
    public DbSet<YarpTimeoutPolicy> TimeoutPolicies => Set<YarpTimeoutPolicy>();
    public DbSet<YarpOutputCachePolicy> OutputCachePolicies => Set<YarpOutputCachePolicy>();
    public DbSet<YarpCorsPolicy> CorsPolicies => Set<YarpCorsPolicy>();
    public DbSet<YarpAuthScheme> AuthSchemes => Set<YarpAuthScheme>();
    public DbSet<YarpAuthPolicy> AuthPolicies => Set<YarpAuthPolicy>();

    // 流量控制
    public DbSet<YarpExperiment> Experiments => Set<YarpExperiment>();
    public DbSet<YarpExperimentVariant> ExperimentVariants => Set<YarpExperimentVariant>();
    public DbSet<YarpExperimentRule> ExperimentRules => Set<YarpExperimentRule>();
    public DbSet<YarpApiVersion> ApiVersions => Set<YarpApiVersion>();

    // 安全性與全域設定
    public DbSet<YarpCertificate> Certificates => Set<YarpCertificate>();
    public DbSet<YarpBlockedPath> BlockedPaths => Set<YarpBlockedPath>();
    public DbSet<YarpIpRule> IpRules => Set<YarpIpRule>();
    public DbSet<YarpGatewaySetting> GatewaySettings => Set<YarpGatewaySetting>();

    // 帳號、版本與稽核
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<YarpConfigVersion> ConfigVersions => Set<YarpConfigVersion>();
    public DbSet<YarpConfigDraft> ConfigDraft => Set<YarpConfigDraft>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureCoreResources(modelBuilder);
        ConfigurePolicies(modelBuilder);
        ConfigureTrafficControl(modelBuilder);
        ConfigureSecurity(modelBuilder);
        ConfigureAccountsAndVersions(modelBuilder);
        ConfigureAuditableColumns(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>新增時寫入 CreatedAt / UpdatedAt，修改時更新 UpdatedAt（UpdatedBy 由 Service 指定）。</summary>
    private void StampTimestamps()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AppUser>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    private static void ConfigureCoreResources(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<YarpDestination>(e =>
        {
            e.HasIndex(x => new { x.ClusterId, x.DestinationKey }).IsUnique();
            e.HasOne<YarpCluster>().WithMany().HasForeignKey(x => x.ClusterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<YarpDestinationHealth>(e =>
        {
            e.HasKey(x => new { x.DestinationId, x.GatewayNode });
            e.HasOne<YarpDestination>().WithMany().HasForeignKey(x => x.DestinationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<YarpCluster>().HasIndex(x => x.ClusterKey).IsUnique();

        modelBuilder.Entity<YarpClusterHealthCheck>(e =>
        {
            e.HasKey(x => x.ClusterId);
            e.Property(x => x.ClusterId).ValueGeneratedNever();
            e.HasOne<YarpCluster>().WithOne().HasForeignKey<YarpClusterHealthCheck>(x => x.ClusterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<YarpClusterSessionAffinity>(e =>
        {
            e.HasKey(x => x.ClusterId);
            e.Property(x => x.ClusterId).ValueGeneratedNever();
            e.HasOne<YarpCluster>().WithOne().HasForeignKey<YarpClusterSessionAffinity>(x => x.ClusterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<YarpService>(e =>
        {
            e.HasIndex(x => x.ServiceKey).IsUnique();
            e.HasOne<YarpCluster>().WithMany().HasForeignKey(x => x.DefaultClusterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpExperiment>().WithMany().HasForeignKey(x => x.DefaultExperimentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpRateLimitPolicy>().WithMany().HasForeignKey(x => x.RateLimitPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpTimeoutPolicy>().WithMany().HasForeignKey(x => x.TimeoutPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpAuthPolicy>().WithMany().HasForeignKey(x => x.AuthPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpCorsPolicy>().WithMany().HasForeignKey(x => x.CorsPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpOutputCachePolicy>().WithMany().HasForeignKey(x => x.OutputCachePolicyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<YarpRoute>(e =>
        {
            e.HasIndex(x => x.RouteKey).IsUnique();
            e.HasIndex(x => x.ServiceId);
            e.HasOne<YarpService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpCluster>().WithMany().HasForeignKey(x => x.ClusterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpExperiment>().WithMany().HasForeignKey(x => x.ExperimentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpRateLimitPolicy>().WithMany().HasForeignKey(x => x.RateLimitPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpTimeoutPolicy>().WithMany().HasForeignKey(x => x.TimeoutPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpAuthPolicy>().WithMany().HasForeignKey(x => x.AuthPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpCorsPolicy>().WithMany().HasForeignKey(x => x.CorsPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpOutputCachePolicy>().WithMany().HasForeignKey(x => x.OutputCachePolicyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<YarpRouteMatchCondition>(e =>
            e.HasOne<YarpRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade));

        modelBuilder.Entity<YarpRouteTransform>(e =>
            e.HasOne<YarpRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade));
    }

    private static void ConfigurePolicies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<YarpRateLimitPolicy>().HasIndex(x => x.PolicyName).IsUnique();
        modelBuilder.Entity<YarpTimeoutPolicy>().HasIndex(x => x.PolicyName).IsUnique();
        modelBuilder.Entity<YarpOutputCachePolicy>().HasIndex(x => x.PolicyName).IsUnique();
        modelBuilder.Entity<YarpCorsPolicy>().HasIndex(x => x.PolicyName).IsUnique();
        modelBuilder.Entity<YarpAuthScheme>().HasIndex(x => x.SchemeName).IsUnique();

        modelBuilder.Entity<YarpAuthPolicy>(e =>
        {
            e.HasIndex(x => x.PolicyName).IsUnique();
            e.HasOne<YarpAuthScheme>().WithMany().HasForeignKey(x => x.AuthSchemeId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTrafficControl(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<YarpExperiment>().HasIndex(x => x.ExperimentKey).IsUnique();

        modelBuilder.Entity<YarpExperimentVariant>(e =>
        {
            e.HasIndex(x => new { x.ExperimentId, x.VariantKey }).IsUnique();
            e.HasOne<YarpExperiment>().WithMany().HasForeignKey(x => x.ExperimentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<YarpCluster>().WithMany().HasForeignKey(x => x.ClusterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<YarpExperimentRule>(e =>
        {
            e.HasOne<YarpExperiment>().WithMany().HasForeignKey(x => x.ExperimentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<YarpExperimentVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<YarpApiVersion>(e =>
        {
            e.HasIndex(x => new { x.ServiceId, x.Version }).IsUnique();
            e.HasOne<YarpService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<YarpCluster>().WithMany().HasForeignKey(x => x.ClusterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<YarpRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureSecurity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<YarpCertificate>().HasIndex(x => x.Thumbprint).IsUnique();
        modelBuilder.Entity<YarpBlockedPath>().HasIndex(x => x.PathPattern).IsUnique();
        modelBuilder.Entity<YarpGatewaySetting>().HasKey(x => x.Section);
    }

    private static void ConfigureAccountsAndVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();

        modelBuilder.Entity<YarpConfigVersion>(e =>
        {
            e.HasKey(x => x.VersionNo);
            // 同一時間只能有一筆 Active（憲章原則一）
            e.HasIndex(x => x.Status).IsUnique().HasFilter($"\"Status\" = '{ConfigVersionStatus.Active}'");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<YarpConfigDraft>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.LastEditedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasData(new YarpConfigDraft { Id = ConfigDraftRules.SingletonId });
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.OccurredAt);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    /// <summary>所有設定表的 UpdatedBy 外鍵 → AppUsers。</summary>
    private static void ConfigureAuditableColumns(ModelBuilder modelBuilder)
    {
        var auditableTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(AuditableEntity).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var clrType in auditableTypes)
        {
            modelBuilder.Entity(clrType)
                .HasOne(typeof(AppUser))
                .WithMany()
                .HasForeignKey(nameof(AuditableEntity.UpdatedBy))
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
