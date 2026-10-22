using ApiGateway.Gateway;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Services.Interfaces;
using ApiGateway.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Tests;

/// <summary>發佈組合器、節點同步狀態、系統路由、排程稽核</summary>
public class SupportServicesTests
{
    private const long UserId = 1;
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);

    private static Mock<IAccessControlService> SignedIn()
    {
        var access = new Mock<IAccessControlService>();
        access.Setup(x => x.EnsureSignedInAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new AppUser { Id = UserId, IsActive = true });
        access.Setup(x => x.EnsureSignedInAsync(null, It.IsAny<CancellationToken>())).ThrowsAsync(new UnauthorizedAccessException(ErrorCodes.Forbidden));
        return access;
    }

    // ---------------------------------------------------------------- PublishComposer

    private static (PublishComposer Sut, Mock<IConfigValidator> Validator) Composer()
    {
        var validator = new Mock<IConfigValidator>();
        validator.Setup(x => x.ValidateRouteAsync(It.IsAny<RouteConfig>())).ReturnsAsync(new List<Exception>());
        validator.Setup(x => x.ValidateClusterAsync(It.IsAny<ClusterConfig>())).ReturnsAsync(new List<Exception>());
        return (new PublishComposer(validator.Object, Options.Create(new GatewayOptions { ReservedPathPrefixes = ["/api/v1"] })), validator);
    }

    private static ConfigSnapshot ValidSnapshot()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        f.AddRoute(f.AddService("svc", clusterId: cluster.Id), "/a");
        return f.Build();
    }

    [Fact]
    public async Task Composer_Valid_ReturnsDocumentAndChecksum()
    {
        var (sut, _) = Composer();

        var result = await sut.ComposeAsync(ValidSnapshot(), Now);

        Assert.Empty(result.Issues);
        Assert.Single(result.Document!.Routes);
        Assert.Matches("^[0-9a-f]{64}$", result.Checksum!);
    }

    [Fact]
    public async Task Composer_RuleIssues_SkipYarpValidation()
    {
        var (sut, validator) = Composer();
        var f = new SnapshotFixture();
        f.AddRoute(f.AddService("svc"), "/api/v1/x");

        var result = await sut.ComposeAsync(f.Build(), Now);

        Assert.Contains(result.Issues, i => i.Code == PublishIssueCodes.RouteReservedPath);
        Assert.Null(result.Document);
        validator.Verify(x => x.ValidateRouteAsync(It.IsAny<RouteConfig>()), Times.Never);
    }

    [Fact]
    public async Task Composer_YarpValidatorErrors_BecomeIssues()
    {
        var (sut, validator) = Composer();
        validator.Setup(x => x.ValidateRouteAsync(It.IsAny<RouteConfig>())).ReturnsAsync(new List<Exception> { new InvalidOperationException("bad route") });
        validator.Setup(x => x.ValidateClusterAsync(It.IsAny<ClusterConfig>())).ReturnsAsync(new List<Exception> { new InvalidOperationException("bad cluster") });

        var result = await sut.ComposeAsync(ValidSnapshot(), Now);

        Assert.Equal(2, result.Issues.Count(i => i.Code == PublishComposer.YarpConfigInvalidCode));
        Assert.Contains(result.Issues, i => i.Params!["message"] == "bad route" && i.RouteKey == "svc-1");
        Assert.Null(result.Checksum);
    }

    // ---------------------------------------------------------------- GatewayNodeService

    [Fact]
    public async Task Nodes_SyncedByChecksum_OnlineByHeartbeat()
    {
        var nodes = new Mock<IGatewayNodeRepository>();
        nodes.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new YarpGatewayNode { NodeId = "gw-01", LoadedVersionNo = "2026102202", LoadedChecksum = "new", LastHeartbeatAt = Now.AddSeconds(-3) },
            new YarpGatewayNode { NodeId = "gw-02", LoadedVersionNo = "2026102201", LoadedChecksum = "old", LastHeartbeatAt = Now.AddMinutes(-5) },
        ]);
        var versions = new Mock<IConfigVersionRepository>();
        versions.Setup(x => x.GetActiveChecksumAsync(It.IsAny<CancellationToken>())).ReturnsAsync(("2026102202", "new"));
        var sut = new GatewayNodeService(SignedIn().Object, nodes.Object, versions.Object, new FakeTimeProvider(Now));

        var result = await sut.ListAsync(UserId);

        Assert.Equal(new GatewayNodeDto("gw-01", "2026102202", true, true, Now.AddSeconds(-3)), result[0]);
        Assert.False(result[1].Synced);
        Assert.False(result[1].Online);
    }

    [Fact]
    public async Task Nodes_NotSignedIn_ThrowsForbidden()
    {
        var sut = new GatewayNodeService(SignedIn().Object, Mock.Of<IGatewayNodeRepository>(), Mock.Of<IConfigVersionRepository>(), TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.ListAsync(null));
    }

    // ---------------------------------------------------------------- SystemRouteService

    [Fact]
    public async Task SystemRoutes_ReadFromSettings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Routes:static-b:Match:Path"] = "/b/{**rest}",
            ["ReverseProxy:Routes:static-b:ClusterId"] = "cluster-b",
            ["ReverseProxy:Routes:static-a:Match:Path"] = "/a",
            ["ReverseProxy:Routes:static-a:ClusterId"] = "cluster-a",
        }).Build();
        var options = Options.Create(new GatewayOptions { HealthCheck = new HealthCheckOptions { Path = "/health" }, ReservedPathPrefixes = ["/health", "/api/v1"] });
        var sut = new SystemRouteService(SignedIn().Object, options, configuration);

        var result = await sut.GetAsync(UserId);

        Assert.Equal("/health", result.HealthCheckPath);
        Assert.Equal(["/health", "/api/v1"], result.ReservedPathPrefixes);
        Assert.Equal([new StaticRouteDto("static-a", "/a", "cluster-a"), new StaticRouteDto("static-b", "/b/{**rest}", "cluster-b")], result.StaticRoutes);
    }

    // ---------------------------------------------------------------- ExperimentScheduleAuditService（G-41）

    private sealed class ScheduleHarness
    {
        public readonly FakeTimeProvider Time = new(Now);
        public readonly Mock<IExperimentRuntimeRepository> Runtime = new();
        public readonly Mock<IAuditTrailService> Audit = new();
        public readonly DatabaseProxyConfigProvider Provider = new();
        public readonly ExperimentScheduleAuditService Sut;

        public ScheduleHarness()
        {
            Runtime.Setup(x => x.TryMarkScheduleEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var services = new ServiceCollection();
            services.AddSingleton(Runtime.Object);
            services.AddSingleton(Audit.Object);
            services.AddSingleton(Mock.Of<IUnitOfWork>());
            Sut = new ExperimentScheduleAuditService(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), Provider,
                new ExperimentRuntimeCache(), Time, NullLogger<ExperimentScheduleAuditService>.Instance);
            Provider.ApplyDocument(new YarpConfigDocument
            {
                Experiments =
                [
                    new ExperimentDefinition { Key = "exp", Status = ExperimentConfigStatus.Running, StartAt = Now.AddMinutes(1), EndAt = Now.AddMinutes(5) },
                ],
            }, "v", "c");
        }
    }

    [Fact]
    public async Task Schedule_RecordsStartAndEndOnceEach()
    {
        var h = new ScheduleHarness();

        await h.Sut.CheckOnceAsync();
        h.Runtime.Verify(x => x.TryMarkScheduleEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);

        h.Time.Advance(TimeSpan.FromMinutes(1));
        await h.Sut.CheckOnceAsync();
        await h.Sut.CheckOnceAsync();
        h.Runtime.Verify(x => x.TryMarkScheduleEventAsync("exp", ExperimentScheduleEvents.Started, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        h.Audit.Verify(x => x.Record(AuditActions.ExperimentScheduleStarted, It.IsAny<string>(), "exp", null, null, It.IsAny<object>()), Times.Once);

        h.Time.Advance(TimeSpan.FromMinutes(4));
        await h.Sut.CheckOnceAsync();
        h.Audit.Verify(x => x.Record(AuditActions.ExperimentScheduleEnded, It.IsAny<string>(), "exp", null, null, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task Schedule_OtherNodeAlreadyRecorded_DoesNotAuditAgain()
    {
        var h = new ScheduleHarness();
        h.Runtime.Setup(x => x.TryMarkScheduleEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        h.Time.Advance(TimeSpan.FromMinutes(1));
        await h.Sut.CheckOnceAsync();

        h.Audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Schedule_DatabaseFailure_RetriesNextRound()
    {
        var h = new ScheduleHarness();
        h.Runtime.SetupSequence(x => x.TryMarkScheduleEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("db down"))
            .ReturnsAsync(true);

        h.Time.Advance(TimeSpan.FromMinutes(1));
        await h.Sut.CheckOnceAsync();
        await h.Sut.CheckOnceAsync();

        h.Audit.Verify(x => x.Record(AuditActions.ExperimentScheduleStarted, It.IsAny<string>(), "exp", null, null, It.IsAny<object>()), Times.Once);
    }
}
