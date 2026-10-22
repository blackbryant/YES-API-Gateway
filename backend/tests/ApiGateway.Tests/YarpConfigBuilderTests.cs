using System.Text.Json;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Tests.Infrastructure;

namespace ApiGateway.Tests;

public class YarpConfigBuilderTests
{
    [Fact]
    public void Build_RouteWithoutTarget_InheritsServiceCluster()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        var service = f.AddService("orders-api", clusterId: cluster.Id);
        f.AddRoute(service, "/api/orders/{**catch-all}", hosts: ["api.example.com"], methods: ["GET", "POST"], order: 50);

        var doc = YarpConfigBuilder.Build(f.Build());

        var route = Assert.Single(doc.Routes);
        Assert.Equal("orders-api-1", route.RouteId);
        Assert.Equal(cluster.ClusterKey, route.ClusterId);
        Assert.Equal(50, route.Order);
        Assert.Equal("/api/orders/{**catch-all}", route.Match.Path);
        Assert.Equal(["api.example.com"], route.Match.Hosts);
        Assert.Equal(["GET", "POST"], route.Match.Methods);
    }

    [Fact]
    public void Build_RouteCluster_OverridesService()
    {
        var f = new SnapshotFixture();
        var serviceCluster = f.AddCluster();
        var routeCluster = f.AddCluster();
        var service = f.AddService("orders-api", clusterId: serviceCluster.Id);
        f.AddRoute(service, "/a", clusterId: routeCluster.Id);

        var doc = YarpConfigBuilder.Build(f.Build());

        Assert.Equal(routeCluster.ClusterKey, Assert.Single(doc.Routes).ClusterId);
    }

    [Fact]
    public void Build_EmptyMethodsAndHosts_AreNull()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        f.AddRoute(f.AddService("svc", clusterId: cluster.Id), "/a");

        var route = Assert.Single(YarpConfigBuilder.Build(f.Build()).Routes);

        Assert.Null(route.Match.Methods);
        Assert.Null(route.Match.Hosts);
    }

    [Fact]
    public void Build_PathRemovePrefix_IsOutputAsTransform()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        var route = f.AddRoute(f.AddService("svc", clusterId: cluster.Id), "/api/orders/{**catch-all}");
        f.AddPrefix(route, "/api/orders");

        var transform = Assert.Single(Assert.Single(YarpConfigBuilder.Build(f.Build()).Routes).Transforms!);

        Assert.Equal("/api/orders", transform["PathRemovePrefix"]);
    }

    [Fact]
    public void Build_InactiveServiceOrRoute_IsNotOutput()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        f.AddRoute(f.AddService("off", clusterId: cluster.Id, active: false), "/a");
        f.AddRoute(f.AddService("on", clusterId: cluster.Id), "/b", active: false);
        f.AddRoute(f.Services[1], "/c");

        var doc = YarpConfigBuilder.Build(f.Build());

        Assert.Equal(["on-2"], doc.Routes.Select(r => r.RouteId));
    }

    [Fact]
    public void Build_InactiveHost_IsNotOutput()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster("http://10.0.0.1:80");
        f.AddHost(cluster, "http://10.0.0.2:80", active: false);
        f.AddRoute(f.AddService("svc", clusterId: cluster.Id), "/a");

        var clusterConfig = Assert.Single(YarpConfigBuilder.Build(f.Build()).Clusters);

        Assert.Equal(cluster.ClusterKey, clusterConfig.ClusterId);
        Assert.Equal(["http://10.0.0.1:80"], clusterConfig.Destinations!.Values.Select(d => d.Address));
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        cluster.Description = "x";
        var service = f.AddService("svc", clusterId: cluster.Id);
        f.AddRoute(service, "/b", order: 2);
        f.AddRoute(service, "/a", order: 1);

        var first = YarpConfigBuilder.Build(f.Build());
        var second = YarpConfigBuilder.Build(f.Build());

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(YarpConfigBuilder.ComputeChecksum(first), YarpConfigBuilder.ComputeChecksum(second));
        Assert.Matches("^[0-9a-f]{64}$", YarpConfigBuilder.ComputeChecksum(first));
    }

    [Fact]
    public void Build_ExperimentTarget_UsesControlClusterAndMetadata()
    {
        var f = new SnapshotFixture();
        var service = f.AddService("orders-api");
        var experiment = f.AddExperiment(service, ("A", 90, true), ("B", 10, false));
        service.DefaultExperimentId = experiment.Id;
        f.AddRoute(service, "/api/orders/{**rest}");
        f.AddForceRule(experiment, "Header", "X-Exp-Group");
        experiment.AutoRollbackRule = """{"metric":"5xxRate","thresholdPercent":2,"durationMin":5,"minSamples":100}""";

        var doc = YarpConfigBuilder.Build(f.Build());

        var route = Assert.Single(doc.Routes);
        Assert.Equal(f.ClusterOf(experiment, "A").ClusterKey, route.ClusterId);
        Assert.Equal("orders-api-ab", route.Metadata!["Yes.Experiment"]);

        var definition = Assert.Single(doc.Experiments);
        Assert.Equal("orders-api-ab", definition.Key);
        Assert.Equal(".yes.exp.orders-api", definition.Sticky.CookieName);
        Assert.Equal([("A", 90, true), ("B", 10, false)], definition.Variants.Select(v => (v.Key, v.Weight, v.IsControl)));
        Assert.Equal(f.ClusterOf(experiment, "B").ClusterKey, definition.Variants[1].ClusterId);
        Assert.Equal("X-Exp-Group", Assert.Single(definition.ForceRules).Name);
        Assert.Equal(2, definition.AutoRollback!.ThresholdPercent);
        Assert.Equal(2, doc.Clusters.Count);
    }

    [Fact]
    public void Build_UnreferencedExperiment_IsNotOutput()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        var service = f.AddService("svc", clusterId: cluster.Id);
        f.AddExperiment(service, ("A", 50, true), ("B", 50, false));
        f.AddRoute(service, "/a");

        Assert.Empty(YarpConfigBuilder.Build(f.Build()).Experiments);
    }
}
