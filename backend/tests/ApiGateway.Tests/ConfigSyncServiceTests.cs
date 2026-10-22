using ApiGateway.Gateway;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApiGateway.Tests;

public class ConfigSyncServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);

    private readonly Mock<IConfigVersionRepository> _versions = new();
    private readonly Mock<IExperimentRuntimeRepository> _runtime = new();
    private readonly Mock<IGatewayNodeRepository> _nodes = new();
    private readonly Mock<IGatewayConfigApplier> _applier = new();
    private readonly ExperimentRuntimeCache _cache = new();
    private readonly ConfigSyncService _sut;

    public ConfigSyncServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_versions.Object);
        services.AddSingleton(_runtime.Object);
        services.AddSingleton(_nodes.Object);
        var provider = services.BuildServiceProvider();

        _runtime.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _sut = new ConfigSyncService(provider.GetRequiredService<IServiceScopeFactory>(), _applier.Object, _cache,
            Options.Create(new GatewayOptions { NodeId = "gw-01" }), new FakeTimeProvider(Now), NullLogger<ConfigSyncService>.Instance);
    }

    private static YarpConfigVersion Version(string versionNo, string checksum)
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        f.AddRoute(f.AddService("svc", clusterId: cluster.Id), "/a");
        return new YarpConfigVersion
        {
            VersionNo = versionNo,
            Status = ConfigVersionStatus.Active,
            Checksum = checksum,
            YarpConfig = YarpConfigBuilder.Serialize(YarpConfigBuilder.Build(f.Build())),
        };
    }

    [Fact]
    public async Task SameChecksum_DoesNotReload()
    {
        _applier.SetupGet(x => x.LoadedChecksum).Returns("abc");
        _versions.Setup(x => x.GetActiveChecksumAsync(It.IsAny<CancellationToken>())).ReturnsAsync(("2026102201", "abc"));

        await _sut.SyncOnceAsync();

        _applier.Verify(x => x.ApplyDocument(It.IsAny<YarpConfigDocument>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _versions.Verify(x => x.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DifferentChecksum_LoadsNewVersionAndWritesHeartbeat()
    {
        _applier.SetupGet(x => x.LoadedChecksum).Returns("old");
        _applier.SetupGet(x => x.LoadedVersionNo).Returns("2026102202");
        _versions.Setup(x => x.GetActiveChecksumAsync(It.IsAny<CancellationToken>())).ReturnsAsync(("2026102202", "new"));
        _versions.Setup(x => x.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Version("2026102202", "new"));

        await _sut.SyncOnceAsync();

        _applier.Verify(x => x.ApplyDocument(It.Is<YarpConfigDocument>(d => d.Routes.Count == 1), "2026102202", "new"), Times.Once);
        _nodes.Verify(x => x.UpsertHeartbeatAsync("gw-01", "2026102202", "old", Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RuntimeOverrides_AreCached()
    {
        _versions.Setup(x => x.GetActiveChecksumAsync(It.IsAny<CancellationToken>())).ReturnsAsync(((string, string)?)null);
        _runtime.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new YarpExperimentRuntime { ExperimentKey = "orders-api-ab", OverrideStatus = ExperimentOverrideStatus.Paused }]);

        await _sut.SyncOnceAsync();

        Assert.Equal(ExperimentOverrideStatus.Paused, _cache.Get("orders-api-ab")?.OverrideStatus);
    }

    [Fact]
    public async Task DatabaseFailure_KeepsCurrentConfig()
    {
        _versions.Setup(x => x.GetActiveChecksumAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("db down"));

        await _sut.SyncOnceAsync();

        _applier.Verify(x => x.ApplyDocument(It.IsAny<YarpConfigDocument>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
