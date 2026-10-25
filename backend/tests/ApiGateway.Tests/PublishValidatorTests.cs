using ApiGateway.Gateway;
using ApiGateway.Models.Constants;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Tests.Infrastructure;

namespace ApiGateway.Tests;

public class PublishValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);

    private static readonly GatewayOptions Options = new()
    {
        ReservedPathPrefixes = ["/health", "/api/v1", "/swagger", "/admin"],
    };

    private static IReadOnlyList<string> Codes(SnapshotFixture f) =>
        PublishValidator.Validate(f.Build(), Options, Now).Select(i => i.Code).ToList();

    private static (SnapshotFixture Fixture, Models.Entities.YarpService Service) WithService()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        return (f, f.AddService("orders-api", clusterId: cluster.Id));
    }

    [Fact]
    public void Validate_ValidRoute_NoIssues()
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/api/orders/{**catch-all}", hosts: ["api.example.com", "*.example.com", "localhost:8080"], methods: ["GET", "PATCH"]);

        Assert.Empty(Codes(f));
    }

    [Theory]
    [InlineData("api/orders")]
    [InlineData("/api/{")]
    [InlineData("")]
    public void Validate_InvalidPath(string path)
    {
        var (f, service) = WithService();
        f.AddRoute(service, path);

        Assert.Contains(PublishIssueCodes.RoutePathInvalid, Codes(f));
    }

    [Theory]
    [InlineData("bad host")]
    [InlineData("http://api.example.com")]
    [InlineData("api..example.com")]
    public void Validate_InvalidHost(string host)
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/a", hosts: [host]);

        Assert.Contains(PublishIssueCodes.RouteHostInvalid, Codes(f));
    }

    [Fact]
    public void Validate_InvalidMethod()
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/a", methods: ["OPTIONS"]);

        Assert.Contains(PublishIssueCodes.RouteMethodInvalid, Codes(f));
    }

    [Fact]
    public void Validate_SamePathHostMethodOrder_Conflicts()
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/api/{id}", methods: ["GET"]);
        var other = f.AddService("other", clusterId: f.Clusters[0].Id);
        f.AddRoute(other, "/API/{key}/", methods: []);

        var issues = PublishValidator.Validate(f.Build(), Options, Now);

        Assert.Equal(2, issues.Count(i => i.Code == PublishIssueCodes.RouteConflict));
    }

    [Theory]
    [InlineData(new[] { "GET" }, new[] { "POST" }, 100, 100)]
    [InlineData(new[] { "GET" }, new[] { "GET" }, 100, 101)]
    public void Validate_DifferentMethodsOrOrder_NoConflict(string[] methodsA, string[] methodsB, int orderA, int orderB)
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/a", methods: methodsA, order: orderA);
        f.AddRoute(service, "/a", methods: methodsB, order: orderB);

        Assert.DoesNotContain(PublishIssueCodes.RouteConflict, Codes(f));
    }

    [Fact]
    public void Validate_DisjointHosts_NoConflict()
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/a", hosts: ["a.example.com"]);
        f.AddRoute(service, "/a", hosts: ["b.example.com"]);

        Assert.DoesNotContain(PublishIssueCodes.RouteConflict, Codes(f));
    }

    [Fact]
    public void Validate_NoTarget()
    {
        var f = new SnapshotFixture();
        f.AddRoute(f.AddService("svc"), "/a");

        Assert.Contains(PublishIssueCodes.RouteNoTarget, Codes(f));
    }

    [Fact]
    public void Validate_MissingOrInactiveCluster()
    {
        var f = new SnapshotFixture();
        var inactive = f.AddCluster(clusterActive: false);
        f.AddRoute(f.AddService("svc", clusterId: inactive.Id), "/a");
        f.AddRoute(f.AddService("svc2", clusterId: 99999), "/b");

        Assert.Equal(2, Codes(f).Count(c => c == PublishIssueCodes.TargetClusterMissing));
    }

    [Fact]
    public void Validate_ClusterWithoutActiveHost()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster(hostActive: false);
        f.AddRoute(f.AddService("svc", clusterId: cluster.Id), "/a");

        Assert.Contains(PublishIssueCodes.TargetClusterNoActiveHost, Codes(f));
    }

    [Fact]
    public void Validate_CollectsAllIssues()
    {
        var f = new SnapshotFixture();
        var service = f.AddService("svc");
        f.AddRoute(service, "bad", hosts: ["bad host"], methods: ["TRACE"]);

        var codes = Codes(f);

        Assert.Contains(PublishIssueCodes.RoutePathInvalid, codes);
        Assert.Contains(PublishIssueCodes.RouteHostInvalid, codes);
        Assert.Contains(PublishIssueCodes.RouteMethodInvalid, codes);
        Assert.Contains(PublishIssueCodes.RouteNoTarget, codes);
    }

    [Fact]
    public void Validate_InactiveRoute_IsIgnored()
    {
        var f = new SnapshotFixture();
        f.AddRoute(f.AddService("svc"), "bad", active: false);

        Assert.Empty(Codes(f));
    }

    // ---------------------------------------------------------------- 系統保留路徑（US3）

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/x")]
    [InlineData("/API/V1/{**rest}")]
    [InlineData("/swagger")]
    [InlineData("/admin/{**path}")]
    public void Validate_ReservedPath_IsRejected(string path)
    {
        var (f, service) = WithService();
        f.AddRoute(service, path, hosts: ["api.example.com"]);

        Assert.Contains(PublishIssueCodes.RouteReservedPath, Codes(f));
    }

    [Theory]
    [InlineData("/api/v2/x")]
    [InlineData("/healthy")]
    [InlineData("/administrator")]
    [InlineData("/api/{**rest}")]
    public void Validate_NonReservedPath_IsAccepted(string path)
    {
        var (f, service) = WithService();
        f.AddRoute(service, path, hosts: ["api.example.com"]);

        Assert.DoesNotContain(PublishIssueCodes.RouteReservedPath, Codes(f));
    }

    [Fact]
    public void Validate_RootCatchAllWithoutHost_IsRejected()
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/{**catch-all}");

        Assert.Contains(PublishIssueCodes.RouteRootCatchAllWithoutHost, Codes(f));
    }

    [Fact]
    public void Validate_RootCatchAllWithHost_IsAccepted()
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/{**catch-all}", hosts: ["www.example.com"]);

        Assert.Empty(Codes(f));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Validate_OrderOutOfRange_IsRejected(int order)
    {
        var (f, service) = WithService();
        f.AddRoute(service, "/a", order: order);

        Assert.Contains(PublishIssueCodes.RouteOrderInvalid, Codes(f));
    }

    [Fact]
    public void Validate_Issue_CarriesServiceAndRoute()
    {
        var f = new SnapshotFixture();
        var service = f.AddService("svc");
        f.AddRoute(service, "/a");

        var issue = Assert.Single(PublishValidator.Validate(f.Build(), Options, Now));

        Assert.Equal(service.Id, issue.ServiceId);
        Assert.Equal("SVC", issue.ServiceName);
        Assert.Equal("svc-1", issue.RouteKey);
    }

    // ---------------------------------------------------------------- A/B 實驗（US4、US6）

    private static (SnapshotFixture Fixture, Models.Entities.YarpExperiment Experiment) WithExperiment(params (string Key, short Weight, bool Control)[] variants)
    {
        var f = new SnapshotFixture();
        var service = f.AddService("orders-api");
        var experiment = f.AddExperiment(service, variants.Length == 0 ? [("A", 90, true), ("B", 10, false)] : variants);
        service.DefaultExperimentId = experiment.Id;
        f.AddRoute(service, "/api/orders/{**rest}");
        return (f, experiment);
    }

    [Fact]
    public void Validate_ValidExperiment_NoIssues() => Assert.Empty(Codes(WithExperiment().Fixture));

    [Fact]
    public void Validate_WeightSumNot100() =>
        Assert.Contains(PublishIssueCodes.ExperimentWeightSum, Codes(WithExperiment(("A", 90, true), ("B", 20, false)).Fixture));

    [Fact]
    public void Validate_TooFewVariants() =>
        Assert.Contains(PublishIssueCodes.ExperimentVariantCount, Codes(WithExperiment(("A", 100, true)).Fixture));

    [Fact]
    public void Validate_TooManyVariants() =>
        Assert.Contains(PublishIssueCodes.ExperimentVariantCount, Codes(WithExperiment(("A", 20, true), ("B", 20, false), ("C", 20, false), ("D", 20, false), ("E", 10, false), ("F", 10, false)).Fixture));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Validate_ControlNotExactlyOne(bool controlA, bool controlB) =>
        Assert.Contains(PublishIssueCodes.ExperimentControl, Codes(WithExperiment(("A", 50, controlA), ("B", 50, controlB)).Fixture));

    [Fact]
    public void Validate_EndBeforeStart()
    {
        var (f, experiment) = WithExperiment();
        experiment.StartAt = Now.AddHours(2);
        experiment.EndAt = Now.AddHours(1);

        Assert.Contains(PublishIssueCodes.ExperimentTimeRange, Codes(f));
    }

    [Fact]
    public void Validate_EndAlreadyPassed()
    {
        var (f, experiment) = WithExperiment();
        experiment.EndAt = Now.AddMinutes(-1);

        Assert.Contains(PublishIssueCodes.ExperimentEnded, Codes(f));
    }

    [Theory]
    [InlineData("""{"metric":"5xxRate","thresholdPercent":0,"durationMin":5,"minSamples":100}""")]
    [InlineData("""{"metric":"5xxRate","thresholdPercent":101,"durationMin":5,"minSamples":100}""")]
    [InlineData("""{"metric":"5xxRate","thresholdPercent":2,"durationMin":0,"minSamples":100}""")]
    [InlineData("""{"metric":"5xxRate","thresholdPercent":2,"durationMin":61,"minSamples":100}""")]
    [InlineData("""{"metric":"5xxRate","thresholdPercent":2,"durationMin":5,"minSamples":0}""")]
    [InlineData("""not json""")]
    public void Validate_InvalidRollbackRule(string json)
    {
        var (f, experiment) = WithExperiment();
        experiment.AutoRollbackRule = json;

        Assert.Contains(PublishIssueCodes.ExperimentRollbackRule, Codes(f));
    }

    [Fact]
    public void Validate_BoundaryRollbackRule_IsValid()
    {
        var (f, experiment) = WithExperiment();
        experiment.AutoRollbackRule = """{"metric":"5xxRate","thresholdPercent":100,"durationMin":60,"minSamples":1}""";

        Assert.Empty(Codes(f));
    }

    // ---------------------------------------------------------------- 警告（不阻擋發佈）

    private static IReadOnlyList<string> WarningCodes(SnapshotFixture f) =>
        PublishValidator.Warn(f.Build()).Select(i => i.Code).ToList();

    [Theory]
    [InlineData("http://backend-a:9001", "backend-a:9001")]
    [InlineData("http://Backend-A:9001/base", "backend-a:9001")]
    [InlineData("http://api.internal", "api.internal")]
    [InlineData("https://api.internal", "api.internal:443")]
    public void Warn_HostSameAsBackendAddress(string address, string host)
    {
        var f = new SnapshotFixture();
        var service = f.AddService("orders-api", clusterId: f.AddCluster(address).Id);
        f.AddRoute(service, "/a", hosts: [host]);

        var warning = Assert.Single(PublishValidator.Warn(f.Build()));
        Assert.Equal(PublishIssueCodes.RouteHostIsBackendAddress, warning.Code);
        Assert.Equal(host, warning.Params!["host"]);
        Assert.Equal(address, warning.Params["address"]);
        // 只是提醒，不列入阻擋發佈的錯誤
        Assert.Empty(Codes(f));
    }

    [Theory]
    [InlineData("http://backend-a:9001", "backend-a")]
    [InlineData("http://backend-a:9001", "backend-a:9002")]
    [InlineData("http://api.internal", "api.internal:8080")]
    public void Warn_DifferentHost_NoWarning(string address, string host)
    {
        var f = new SnapshotFixture();
        var service = f.AddService("orders-api", clusterId: f.AddCluster(address).Id);
        f.AddRoute(service, "/a", hosts: [host]);

        Assert.Empty(WarningCodes(f));
    }

    [Fact]
    public void Warn_InactiveBackendOrRoute_NoWarning()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster("http://backend-a:9001", hostActive: false);
        f.AddHost(cluster, "http://backend-b:9002");
        var service = f.AddService("orders-api", clusterId: cluster.Id);
        f.AddRoute(service, "/a", hosts: ["backend-a:9001"]);
        f.AddRoute(service, "/b", hosts: ["backend-b:9002"], active: false);

        Assert.Empty(WarningCodes(f));
    }
}
