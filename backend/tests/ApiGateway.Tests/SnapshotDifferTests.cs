using ApiGateway.Models.DTOs;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Tests.Infrastructure;

namespace ApiGateway.Tests;

public class SnapshotDifferTests
{
    private static SnapshotFixture Baseline()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster();
        f.AddRoute(f.AddService("orders-api", clusterId: cluster.Id), "/api/orders");
        return f;
    }

    [Fact]
    public void Diff_NoActiveVersion_AllAdded()
    {
        var changes = SnapshotDiffer.Diff(null, Baseline().Build());

        Assert.All(changes, c => Assert.Equal(ChangeAction.Added, c.Action));
        Assert.Contains(changes, c => c is { EntityType: ChangeEntityType.Service, EntityKey: "orders-api" });
        Assert.Contains(changes, c => c is { EntityType: ChangeEntityType.Route, EntityKey: "orders-api-1" });
        Assert.Contains(changes, c => c.EntityType == ChangeEntityType.Cluster);
        Assert.Contains(changes, c => c.EntityType == ChangeEntityType.Host);
    }

    [Fact]
    public void Diff_Identical_NoChanges()
    {
        var f = Baseline();

        Assert.Empty(SnapshotDiffer.Diff(f.Build(), f.Build()));
    }

    [Fact]
    public void Diff_OnlyAuditFieldsChanged_NoChanges()
    {
        var f = Baseline();
        var before = ConfigSnapshot.Deserialize(f.Build().Serialize());
        f.Routes[0].UpdatedAt = DateTimeOffset.UtcNow;
        f.Routes[0].UpdatedBy = 99;

        Assert.Empty(SnapshotDiffer.Diff(before, f.Build()));
    }

    [Fact]
    public void Diff_ModifiedRoute_AndTransformCountsAsRoute()
    {
        var f = Baseline();
        var before = ConfigSnapshot.Deserialize(f.Build().Serialize());
        f.AddPrefix(f.Routes[0], "/api");

        var change = Assert.Single(SnapshotDiffer.Diff(before, f.Build()));

        Assert.Equal(ChangeEntityType.Route, change.EntityType);
        Assert.Equal(ChangeAction.Modified, change.Action);
        Assert.Equal(f.Services[0].Id, change.ServiceId);
        Assert.Equal("ORDERS-API", change.ServiceName);
    }

    [Fact]
    public void Diff_RemovedService_ReportsRemovedWithName()
    {
        var f = Baseline();
        var before = ConfigSnapshot.Deserialize(f.Build().Serialize());

        var changes = SnapshotDiffer.Diff(before, ConfigSnapshot.Empty);

        Assert.All(changes, c => Assert.Equal(ChangeAction.Removed, c.Action));
        Assert.Contains(changes, c => c is { EntityType: ChangeEntityType.Service, ServiceName: "ORDERS-API" });
    }

    [Fact]
    public void Diff_ExperimentWeightChange_IsExperimentModified()
    {
        var f = new SnapshotFixture();
        var service = f.AddService("orders-api");
        var experiment = f.AddExperiment(service, ("A", 90, true), ("B", 10, false));
        service.DefaultExperimentId = experiment.Id;
        var before = ConfigSnapshot.Deserialize(f.Build().Serialize());
        f.Variants[0].WeightPercent = 80;
        f.Variants[1].WeightPercent = 20;

        var change = Assert.Single(SnapshotDiffer.Diff(before, f.Build()));

        Assert.Equal(ChangeEntityType.Experiment, change.EntityType);
        Assert.Equal("orders-api-ab", change.EntityKey);
    }
}
