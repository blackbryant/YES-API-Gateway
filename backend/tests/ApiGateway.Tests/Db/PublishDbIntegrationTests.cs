using ApiGateway.Gateway;
using ApiGateway.Infrastructure.Data;
using ApiGateway.Infrastructure.Ids;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Implementations;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Services.Interfaces;
using ApiGateway.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Tests.Db;

/// <summary>
/// 需實際 PostgreSQL（GATEWAY_TEST_DB）。每次測試以全新 schema 建立資料庫，結束後刪除。
/// </summary>
public sealed class PublishDbIntegrationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private long _adminId;

    public PublishDbIntegrationTests()
    {
        var baseConnection = DbFactAttribute.ConnectionString ?? string.Empty;
        _connectionString = baseConnection.Length == 0
            ? string.Empty
            : new Npgsql.NpgsqlConnectionStringBuilder(baseConnection) { Database = $"yes_gw_test_{Guid.NewGuid():N}" }.ConnectionString;
    }

    private GatewayDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<GatewayDbContext>().UseNpgsql(_connectionString).Options, TimeProvider.System);

    public async Task InitializeAsync()
    {
        if (_connectionString.Length == 0)
        {
            return;
        }

        await using var db = CreateDb();
        await db.Database.MigrateAsync();
        var admin = new AppUser { Username = "admin", Role = RoleCodes.Admin, IsActive = true, PasswordHash = "x" };
        db.AppUsers.Add(admin);
        var cluster = new YarpCluster
        {
            ClusterKey = new UlidGenerator(TimeProvider.System).NewUlid(),
            DisplayName = "Orders",
            LoadBalancingPolicy = ClusterRules.DefaultLoadBalancingPolicy,
            HttpVersion = ClusterRules.DefaultHttpVersion,
            HttpVersionPolicy = ClusterRules.DefaultHttpVersionPolicy,
            ActivityTimeoutSec = ClusterRules.DefaultActivityTimeoutSec,
            IsActive = true,
        };
        db.Clusters.Add(cluster);
        await db.SaveChangesAsync();
        db.Destinations.Add(new YarpDestination { ClusterId = cluster.Id, DestinationKey = "d-1", Address = "http://127.0.0.1:9001", IsActive = true });
        var service = new YarpService { ServiceKey = "orders-api", DisplayName = "Orders", DefaultClusterId = cluster.Id, IsActive = true };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        db.Routes.Add(new YarpRoute { ServiceId = service.Id, RouteKey = "orders-api-1", MatchPath = "/api/orders/{**catch-all}", Order = RouteRules.DefaultOrder, IsActive = true });
        var draft = await db.ConfigDraft.SingleAsync();
        draft.IsDirty = true;
        draft.ChangeCount = 1;
        draft.LastEditedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        _adminId = admin.Id;
    }

    public async Task DisposeAsync()
    {
        if (_connectionString.Length == 0)
        {
            return;
        }

        await using var db = CreateDb();
        await db.Database.EnsureDeletedAsync();
    }

    private PublishService CreateService(GatewayDbContext db, IGatewayConfigApplier applier)
    {
        var admin = new AppUser { Id = _adminId, Role = RoleCodes.Admin, IsActive = true };
        var access = new Mock<IAccessControlService>();
        access.Setup(x => x.EnsureAdminAsync(_adminId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        var options = Options.Create(new GatewayOptions { ReservedPathPrefixes = ["/health", "/api/v1"] });
        var validator = new Mock<IConfigValidator>();
        validator.Setup(x => x.ValidateRouteAsync(It.IsAny<RouteConfig>())).ReturnsAsync(new List<Exception>());
        validator.Setup(x => x.ValidateClusterAsync(It.IsAny<ClusterConfig>())).ReturnsAsync(new List<Exception>());

        return new PublishService(
            access.Object,
            new ConfigSnapshotRepository(db),
            new ConfigVersionRepository(db),
            new AuditTrailService(new AuditLogRepository(db), new Microsoft.AspNetCore.Http.HttpContextAccessor(), TimeProvider.System),
            new UnitOfWork(db),
            new PublishComposer(validator.Object, options),
            applier,
            TimeProvider.System,
            NullLogger<PublishService>.Instance);
    }

    private async Task<string> DraftTokenAsync()
    {
        await using var db = CreateDb();
        return PublishService.ToDraftToken(await db.ConfigDraft.AsNoTracking().SingleAsync());
    }

    [DbFact]
    public async Task Publish_WritesActiveVersionWithChecksum()
    {
        await using var db = CreateDb();
        var applier = new Mock<IGatewayConfigApplier>();

        var result = await CreateService(db, applier.Object).PublishAsync(_adminId, new PublishRequest("first", await DraftTokenAsync()));

        await using var verify = CreateDb();
        var version = await verify.ConfigVersions.SingleAsync();
        Assert.Equal(result.VersionNo, version.VersionNo);
        Assert.Equal(ConfigVersionStatus.Active, version.Status);
        Assert.Matches("^[0-9a-f]{64}$", version.Checksum);
        Assert.False((await verify.ConfigDraft.SingleAsync()).IsDirty);
        applier.Verify(x => x.ApplyDocument(It.IsAny<YarpConfigDocument>(), result.VersionNo, version.Checksum), Times.Once);
    }

    [DbFact]
    public async Task Publish_Concurrent_OnlyOneSucceeds()
    {
        var token = await DraftTokenAsync();
        await using var holder = CreateDb();
        await using var tx = await holder.Database.BeginTransactionAsync();
        await holder.ConfigDraft.FromSql($"SELECT * FROM \"YarpConfigDraft\" WHERE \"Id\" = {ConfigDraftRules.SingletonId} FOR UPDATE").ToListAsync();

        await using var db = CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(db, Mock.Of<IGatewayConfigApplier>()).PublishAsync(_adminId, new PublishRequest("second", token)));

        Assert.Equal(ErrorCodes.PublishInProgress, ex.Message);
    }
}
