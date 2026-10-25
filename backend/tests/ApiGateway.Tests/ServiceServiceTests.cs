using ApiGateway.Gateway.Experiments;
using ApiGateway.Infrastructure.Ids;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations;
using ApiGateway.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ApiGateway.Tests;

public class ServiceServiceTests
{
    private const long AdminId = 1;
    private const long ReaderId = 2;

    private readonly Mock<IAccessControlService> _access = new();
    private readonly Mock<IServiceRepository> _repo = new();
    private readonly Mock<IConfigDraftRepository> _drafts = new();
    private readonly Mock<IAuditTrailService> _audit = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISystemIdGenerator> _ids = new();
    private readonly Mock<IServiceReadSupport> _read = new();
    private readonly List<object> _added = [];
    private readonly List<object> _removed = [];
    private readonly ServiceService _sut;
    private long _nextId = 1000;
    private int _ulidSeq;

    public ServiceServiceTests()
    {
        var admin = new AppUser { Id = AdminId, Username = "admin", Role = RoleCodes.Admin, IsActive = true };
        _access.Setup(x => x.EnsureAdminAsync(AdminId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _access.Setup(x => x.EnsureAdminAsync(ReaderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException(ErrorCodes.Forbidden));
        _access.Setup(x => x.EnsureSignedInAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        _ids.Setup(x => x.NewUlid()).Returns(() => $"01JC{++_ulidSeq:D22}");
        _read.Setup(x => x.GetExperimentState(It.IsAny<string>())).Returns(new ExperimentState(ExperimentStatus.Draft, false, null, null));
        _unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IDbContextTransaction>());

        // 模擬資料庫產生流水號
        _repo.Setup(x => x.Add(It.IsAny<object>())).Callback<object>(e =>
        {
            var idProperty = e.GetType().GetProperty("Id");
            if (idProperty?.PropertyType == typeof(long) && (long)idProperty.GetValue(e)! == 0)
            {
                idProperty.SetValue(e, ++_nextId);
            }

            _added.Add(e);
        });
        _repo.Setup(x => x.Remove(It.IsAny<object>())).Callback<object>(_removed.Add);

        _sut = new ServiceService(_access.Object, _repo.Object, _drafts.Object, _audit.Object, _unitOfWork.Object,
            _ids.Object, _read.Object, NullLogger<ServiceService>.Instance);
    }

    private static ServiceSaveRequest Request(
        string key = "orders-api",
        List<RouteInput>? routes = null,
        List<HostInput>? hosts = null) =>
        new(key, "訂單 API", ["payment"], true, null,
            routes ?? [new RouteInput(null, "/api/orders/{**catch-all}", ["API.example.com"], ["get", "POST", "GET"], null, "/api/orders", true, null)],
            RoutingMode.Single,
            hosts ?? [new HostInput(null, "http://10.0.0.1:8080", true)],
            null);

    private static ExperimentInput Experiment(int weightA = 90, int weightB = 10, int stickyDays = 7) =>
        new(true, null, stickyDays, null, null, null,
            [
                new VariantInput("A", weightA, true, [new HostInput(null, "http://10.0.0.1:8080", true)]),
                new VariantInput("B", weightB, false, [new HostInput(null, "http://10.0.0.2:8080", true)]),
            ],
            [new ForceRuleInput("Header", "X-Exp-Group", null, null)]);

    private T Added<T>() => _added.OfType<T>().Single();

    [Fact]
    public async Task Create_ByReader_ThrowsForbidden()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.CreateAsync(ReaderId, Request()));

        Assert.Equal(ErrorCodes.Forbidden, ex.Message);
        Assert.Empty(_added);
    }

    [Theory]
    [InlineData("Orders")]
    [InlineData("-orders")]
    [InlineData("o")]
    [InlineData("orders_api")]
    public async Task Create_InvalidServiceKey_ThrowsArgument(string key)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, Request(key)));

        Assert.Equal(ErrorCodes.ServicesKeyInvalid, ex.Message);
    }

    [Fact]
    public async Task Create_DuplicateKey_ThrowsConflict()
    {
        _repo.Setup(x => x.ExistsByKeyAsync("orders-api", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateAsync(AdminId, Request()));

        Assert.Equal(ErrorCodes.ServicesKeyExists, ex.Message);
    }

    [Fact]
    public async Task Create_NoRoutes_ThrowsArgument()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, Request(routes: [])));

        Assert.Equal(ErrorCodes.ServicesRoutesRequired, ex.Message);
    }

    [Fact]
    public async Task Create_SingleWithoutHosts_ThrowsArgument()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, Request(hosts: [])));

        Assert.Equal(ErrorCodes.ServicesHostsRequired, ex.Message);
    }

    [Theory]
    [InlineData("ftp://10.0.0.1")]
    [InlineData("10.0.0.1:8080")]
    public async Task Create_InvalidHostAddress_ThrowsArgument(string address)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateAsync(AdminId, Request(hosts: [new HostInput(null, address, true)])));

        Assert.Equal(ErrorCodes.HostsAddressInvalid, ex.Message);
    }

    [Theory]
    [InlineData("/a", null, new[] { "OPTIONS" }, null, ErrorCodes.RoutesMethodInvalid)]
    [InlineData("/a", null, null, -1, ErrorCodes.RoutesOrderInvalid)]
    [InlineData("/a", null, null, 10001, ErrorCodes.RoutesOrderInvalid)]
    [InlineData("a", null, null, null, ErrorCodes.RoutesPathInvalid)]
    [InlineData("/a", "bad host", null, null, ErrorCodes.RoutesHostInvalid)]
    public async Task Create_InvalidRoute_ThrowsArgument(string path, string? host, string[]? methods, int? order, string expected)
    {
        var route = new RouteInput(null, path, host is null ? null : [host], methods, order, null, true, null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, Request(routes: [route])));

        Assert.Equal(expected, ex.Message);
    }

    [Theory]
    [InlineData("/api/other")]
    [InlineData("/api/orders/{id}")]
    [InlineData("api/orders")]
    public async Task Create_PrefixNotLiteralPrefix_ThrowsArgument(string prefix)
    {
        var route = new RouteInput(null, "/api/orders/{**catch-all}", null, null, null, prefix, true, null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, Request(routes: [route])));

        Assert.Equal(ErrorCodes.RoutesPrefixInvalid, ex.Message);
    }

    [Fact]
    public async Task Create_Single_CreatesClusterRoutesTransformAndHosts()
    {
        var request = Request(routes:
        [
            new RouteInput(null, "/api/orders/{**catch-all}", ["API.example.com"], ["get", "POST", "GET"], null, "/api/orders", true, null),
            new RouteInput(null, "/v2/orders", null, null, 50, null, true, null),
        ]);

        var result = await _sut.CreateAsync(AdminId, request);

        var service = Added<YarpService>();
        var cluster = Added<YarpCluster>();
        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{26}$", cluster.ClusterKey);
        Assert.Equal("訂單 API", cluster.DisplayName);
        Assert.Equal(service.Id, cluster.OwnerServiceId);
        Assert.Equal(cluster.Id, service.DefaultClusterId);
        Assert.Null(service.DefaultExperimentId);

        var routes = _added.OfType<YarpRoute>().ToList();
        Assert.Equal(["orders-api-1", "orders-api-2"], routes.Select(r => r.RouteKey));
        Assert.Equal(["GET", "POST"], routes[0].MatchMethods);
        Assert.Equal(["api.example.com"], routes[0].MatchHosts);
        Assert.Equal(RouteRules.DefaultOrder, routes[0].Order);
        Assert.Equal(50, routes[1].Order);

        var transform = Added<YarpRouteTransform>();
        Assert.Equal(routes[0].Id, transform.RouteId);
        Assert.Equal(RouteRules.TransformPathRemovePrefix, transform.TransformType);
        Assert.Contains("\"PathRemovePrefix\":\"/api/orders\"", transform.Definition);

        var host = Added<YarpDestination>();
        Assert.Equal(cluster.Id, host.ClusterId);
        Assert.StartsWith(RouteRules.DestinationKeyPrefix, host.DestinationKey);

        Assert.Equal(RoutingMode.Single, result.RoutingMode);
        Assert.Equal("/api/orders", result.Routes.Single(r => r.RouteKey == "orders-api-1").PathRemovePrefix);
        Assert.Equal(cluster.ClusterKey, result.Cluster!.ClusterKey);
        _drafts.Verify(x => x.MarkDirtyAsync(AdminId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(x => x.Record(AuditActions.Create, "YarpServices", "orders-api", AdminId, null, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task Update_ChangeServiceKey_ThrowsImmutable()
    {
        SetupExisting(SingleAggregate());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.UpdateAsync(AdminId, 10, Request("other-api")));

        Assert.Equal(ErrorCodes.ServicesKeyImmutable, ex.Message);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNull()
    {
        Assert.Null(await _sut.UpdateAsync(AdminId, 99, Request()));
    }

    [Fact]
    public async Task Update_RemovedRouteAndNewRoute_KeepsKeysStable()
    {
        var aggregate = SingleAggregate();
        SetupExisting(aggregate);

        await _sut.UpdateAsync(AdminId, 10, Request(routes:
        [
            new RouteInput(21, "/api/orders/{**catch-all}", null, null, null, null, true, null),
            new RouteInput(null, "/new", null, null, null, null, true, null),
        ]));

        Assert.Contains(_removed, e => e is YarpRoute { Id: 22 });
        Assert.Equal("orders-api-3", Added<YarpRoute>().RouteKey);
    }

    [Fact]
    public async Task Delete_ClusterReferencedElsewhere_ThrowsConflict()
    {
        SetupExisting(SingleAggregate());
        _repo.Setup(x => x.IsClusterReferencedOutsideAsync(It.IsAny<IReadOnlyCollection<long>>(), 10, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeleteAsync(AdminId, 10));

        Assert.Equal(ErrorCodes.ClustersInUse, ex.Message);
        Assert.Empty(_removed);
    }

    [Fact]
    public async Task Delete_RemovesRoutesOwnedClustersAndHosts()
    {
        var aggregate = SingleAggregate();
        SetupExisting(aggregate);

        Assert.True(await _sut.DeleteAsync(AdminId, 10));

        Assert.Contains(aggregate.Service, _removed);
        Assert.All(aggregate.Routes, r => Assert.Contains(r, _removed));
        Assert.All(aggregate.Clusters, c => Assert.Contains(c, _removed));
        Assert.All(aggregate.Destinations, d => Assert.Contains(d, _removed));
        _audit.Verify(x => x.Record(AuditActions.Delete, "YarpServices", "orders-api", AdminId, It.IsAny<object>(), null), Times.Once);
    }

    [Fact]
    public async Task SetActive_MarksDraftAndAudits()
    {
        var aggregate = SingleAggregate();
        SetupExisting(aggregate);

        Assert.True(await _sut.SetActiveAsync(AdminId, 10, false));

        Assert.False(aggregate.Service.IsActive);
        _drafts.Verify(x => x.MarkDirtyAsync(AdminId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------- A/B（US4）

    [Fact]
    public async Task Create_AbTest_CreatesClusterPerVariantAndRules()
    {
        var request = Request() with { RoutingMode = RoutingMode.AbTest, Hosts = null, Experiment = Experiment() };

        var result = await _sut.CreateAsync(AdminId, request);

        var service = Added<YarpService>();
        var experiment = Added<YarpExperiment>();
        Assert.Equal("orders-api-ab", experiment.ExperimentKey);
        Assert.Equal(".yes.exp.orders-api", experiment.StickyCookieName);
        Assert.Equal(ExperimentConfigStatus.Running, experiment.Status);
        Assert.Equal(experiment.Id, service.DefaultExperimentId);
        Assert.Null(service.DefaultClusterId);

        var clusters = _added.OfType<YarpCluster>().ToList();
        Assert.Equal(["訂單 API · A", "訂單 API · B"], clusters.Select(c => c.DisplayName).Order());
        var variants = _added.OfType<YarpExperimentVariant>().ToList();
        Assert.Equal(2, variants.Count);
        Assert.Single(variants, v => v.IsControl && v.VariantKey == "A");
        Assert.Equal(2, _added.OfType<YarpDestination>().Count());
        var rule = Added<YarpExperimentRule>();
        Assert.Equal(ExperimentRuleTypes.ForceVariant, rule.Action);
        Assert.Equal("X-Exp-Group", rule.MatchName);

        Assert.Equal(RoutingMode.AbTest, result.RoutingMode);
        Assert.Equal(2, result.Experiment!.Variants.Count);
    }

    [Theory]
    [InlineData(90, 20, 7)]
    [InlineData(90, 10, 366)]
    [InlineData(90, 10, -1)]
    public async Task Create_AbTest_InvalidExperiment_ThrowsArgument(int a, int b, int stickyDays)
    {
        var request = Request() with { RoutingMode = RoutingMode.AbTest, Experiment = Experiment(a, b, stickyDays) };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(AdminId, request));

        Assert.Equal(ErrorCodes.ExperimentsInvalid, ex.Message);
    }

    [Fact]
    public async Task Create_AbTestWithoutExperiment_ThrowsArgument()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateAsync(AdminId, Request() with { RoutingMode = RoutingMode.AbTest }));

        Assert.Equal(ErrorCodes.ServicesRoutingModeInvalid, ex.Message);
    }

    [Fact]
    public async Task Update_SingleToAbTest_ReusesClusterAsControl()
    {
        var aggregate = SingleAggregate();
        var mainCluster = aggregate.Clusters.Single();
        SetupExisting(aggregate);

        await _sut.UpdateAsync(AdminId, 10, Request() with { RoutingMode = RoutingMode.AbTest, Experiment = Experiment() });

        var control = _added.OfType<YarpExperimentVariant>().Single(v => v.IsControl);
        Assert.Equal(mainCluster.Id, control.ClusterId);
        Assert.Single(_added.OfType<YarpCluster>());
        Assert.DoesNotContain(mainCluster, _removed);
    }

    [Fact]
    public async Task Update_AbTestToSingle_KeepsControlClusterAndRemovesOthers()
    {
        var aggregate = AbAggregate();
        SetupExisting(aggregate);
        var controlCluster = aggregate.Clusters.Single(c => c.Id == 31);
        var otherCluster = aggregate.Clusters.Single(c => c.Id == 32);
        var experiment = aggregate.Experiment!;

        await _sut.UpdateAsync(AdminId, 10, Request(hosts: [new HostInput(41, "http://10.0.0.1:8080", true)]));

        Assert.Equal(controlCluster.Id, aggregate.Service.DefaultClusterId);
        Assert.Null(aggregate.Service.DefaultExperimentId);
        Assert.Contains(experiment, _removed);
        Assert.Contains(otherCluster, _removed);
        Assert.DoesNotContain(controlCluster, _removed);
    }

    // ---------------------------------------------------------------- 查詢（US5）

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, PagingRules.MaxPageSize + 1)]
    public async Task Search_InvalidPaging_ThrowsArgument(int pageIndex, int pageSize)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.SearchAsync(AdminId, new ServiceQuery(null, null, pageIndex, pageSize)));

        Assert.Equal(ErrorCodes.PagingInvalid, ex.Message);
    }

    [Fact]
    public async Task Search_MapsRowsAndUnpublishedFlag()
    {
        var publishedAt = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        _read.Setup(x => x.GetActivePublishedAtAsync(It.IsAny<CancellationToken>())).ReturnsAsync(publishedAt);
        var changed = new YarpService { Id = 1, ServiceKey = "a", DisplayName = "A", UpdatedAt = publishedAt.AddMinutes(1), DefaultClusterId = 5 };
        var unchanged = new YarpService { Id = 2, ServiceKey = "b", DisplayName = "B", UpdatedAt = publishedAt.AddMinutes(-1), DefaultExperimentId = 7 };
        _repo.Setup(x => x.SearchAsync("ord", "payment", ServiceSortField.Host, true, 20, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(([new ServiceListRow(changed, 2, ["api.example.com"], ["/api/orders/{**catch-all}"]), new ServiceListRow(unchanged, 1, [], [])], 22));

        var result = await _sut.SearchAsync(AdminId, new ServiceQuery("ord", "payment", 2, 20, ServiceSortField.Host, SortDesc: true));

        Assert.Equal(22, result.TotalCount);
        Assert.True(result.Data[0].HasUnpublishedChanges);
        Assert.False(result.Data[1].HasUnpublishedChanges);
        Assert.Equal(RoutingMode.AbTest, result.Data[1].RoutingMode);
        Assert.Equal(2, result.Data[0].RouteCount);
        Assert.Equal(["/api/orders/{**catch-all}"], result.Data[0].Paths);
    }

    [Fact]
    public async Task Search_UndefinedSortField_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.SearchAsync(AdminId, new ServiceQuery(null, null, SortBy: (ServiceSortField)99)));

        Assert.Equal(ErrorCodes.ValidationFailed, ex.Message);
    }

    // ---------------------------------------------------------------- 測試資料

    private void SetupExisting(ServiceAggregate aggregate) =>
        _repo.Setup(x => x.GetAggregateAsync(10, true, It.IsAny<CancellationToken>())).ReturnsAsync(aggregate);

    private static ServiceAggregate SingleAggregate()
    {
        var service = new YarpService { Id = 10, ServiceKey = "orders-api", DisplayName = "訂單 API", DefaultClusterId = 31, IsActive = true };
        var cluster = new YarpCluster { Id = 31, ClusterKey = "01JC0000000000000000000031", DisplayName = "訂單 API", OwnerServiceId = 10, IsActive = true };
        return new ServiceAggregate(
            service,
            [
                new YarpRoute { Id = 21, ServiceId = 10, RouteKey = "orders-api-1", MatchPath = "/api/orders/{**catch-all}" },
                new YarpRoute { Id = 22, ServiceId = 10, RouteKey = "orders-api-2", MatchPath = "/old" },
            ],
            [],
            [cluster],
            [new YarpDestination { Id = 41, ClusterId = 31, DestinationKey = "d-1", Address = "http://10.0.0.1:8080", IsActive = true }],
            null,
            [],
            []);
    }

    private static ServiceAggregate AbAggregate()
    {
        var service = new YarpService { Id = 10, ServiceKey = "orders-api", DisplayName = "訂單 API", DefaultExperimentId = 50, IsActive = true };
        var experiment = new YarpExperiment { Id = 50, ServiceId = 10, ExperimentKey = "orders-api-ab", Status = ExperimentConfigStatus.Running };
        return new ServiceAggregate(
            service,
            [new YarpRoute { Id = 21, ServiceId = 10, RouteKey = "orders-api-1", MatchPath = "/api/orders/{**catch-all}" }],
            [],
            [
                new YarpCluster { Id = 31, ClusterKey = "01JC0000000000000000000031", DisplayName = "訂單 API · A", OwnerServiceId = 10, IsActive = true },
                new YarpCluster { Id = 32, ClusterKey = "01JC0000000000000000000032", DisplayName = "訂單 API · B", OwnerServiceId = 10, IsActive = true },
            ],
            [
                new YarpDestination { Id = 41, ClusterId = 31, DestinationKey = "d-1", Address = "http://10.0.0.1:8080", IsActive = true },
                new YarpDestination { Id = 42, ClusterId = 32, DestinationKey = "d-2", Address = "http://10.0.0.2:8080", IsActive = true },
            ],
            experiment,
            [
                new YarpExperimentVariant { Id = 61, ExperimentId = 50, VariantKey = "A", ClusterId = 31, WeightPercent = 90, IsControl = true },
                new YarpExperimentVariant { Id = 62, ExperimentId = 50, VariantKey = "B", ClusterId = 32, WeightPercent = 10 },
            ],
            []);
    }
}
