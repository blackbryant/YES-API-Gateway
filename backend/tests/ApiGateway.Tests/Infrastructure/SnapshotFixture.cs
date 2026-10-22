using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;
using ApiGateway.Services.Implementations.Publishing;

namespace ApiGateway.Tests.Infrastructure;

/// <summary>建立測試用 ConfigSnapshot 的輔助類別</summary>
public sealed class SnapshotFixture
{
    private long _id = 100;

    public List<YarpService> Services { get; } = [];
    public List<YarpRoute> Routes { get; } = [];
    public List<YarpRouteTransform> Transforms { get; } = [];
    public List<YarpCluster> Clusters { get; } = [];
    public List<YarpDestination> Destinations { get; } = [];
    public List<YarpExperiment> Experiments { get; } = [];
    public List<YarpExperimentVariant> Variants { get; } = [];
    public List<YarpExperimentRule> Rules { get; } = [];

    private long NextId() => ++_id;

    public YarpCluster AddCluster(string? address = "http://10.0.0.1:8080", bool hostActive = true, bool clusterActive = true, long? ownerServiceId = null)
    {
        var id = NextId();
        var cluster = new YarpCluster
        {
            Id = id,
            ClusterKey = $"01JC{id:D22}",
            DisplayName = $"Cluster {id}",
            LoadBalancingPolicy = ClusterRules.DefaultLoadBalancingPolicy,
            HttpVersion = ClusterRules.DefaultHttpVersion,
            HttpVersionPolicy = ClusterRules.DefaultHttpVersionPolicy,
            ActivityTimeoutSec = ClusterRules.DefaultActivityTimeoutSec,
            IsActive = clusterActive,
            OwnerServiceId = ownerServiceId,
        };
        Clusters.Add(cluster);
        if (address is not null)
        {
            AddHost(cluster, address, hostActive);
        }

        return cluster;
    }

    public YarpDestination AddHost(YarpCluster cluster, string address, bool active = true)
    {
        var id = NextId();
        var destination = new YarpDestination
        {
            Id = id,
            ClusterId = cluster.Id,
            DestinationKey = $"d-{id:D24}",
            Address = address,
            IsActive = active,
        };
        Destinations.Add(destination);
        return destination;
    }

    public YarpService AddService(string key, long? clusterId = null, long? experimentId = null, bool active = true)
    {
        var service = new YarpService
        {
            Id = NextId(),
            ServiceKey = key,
            DisplayName = key.ToUpperInvariant(),
            DefaultClusterId = clusterId,
            DefaultExperimentId = experimentId,
            IsActive = active,
        };
        Services.Add(service);
        return service;
    }

    public YarpRoute AddRoute(
        YarpService service,
        string path,
        string[]? hosts = null,
        string[]? methods = null,
        int order = RouteRules.DefaultOrder,
        long? clusterId = null,
        long? experimentId = null,
        bool active = true)
    {
        var seq = Routes.Count(r => r.ServiceId == service.Id) + 1;
        var route = new YarpRoute
        {
            Id = NextId(),
            ServiceId = service.Id,
            RouteKey = $"{service.ServiceKey}-{seq}",
            MatchPath = path,
            MatchHosts = hosts ?? [],
            MatchMethods = methods ?? [],
            Order = order,
            ClusterId = clusterId,
            ExperimentId = experimentId,
            IsActive = active,
        };
        Routes.Add(route);
        return route;
    }

    public void AddPrefix(YarpRoute route, string prefix) =>
        Transforms.Add(new YarpRouteTransform
        {
            Id = NextId(),
            RouteId = route.Id,
            Direction = RouteRules.TransformDirectionRequest,
            TransformType = RouteRules.TransformPathRemovePrefix,
            Definition = $$"""{"PathRemovePrefix":"{{prefix}}"}""",
        });

    /// <summary>建立實驗，每組各自建立一個群組（含一台主機）</summary>
    public YarpExperiment AddExperiment(YarpService service, params (string Key, short Weight, bool Control)[] variants)
    {
        var experiment = new YarpExperiment
        {
            Id = NextId(),
            ServiceId = service.Id,
            ExperimentKey = service.ServiceKey + ExperimentRules.ExperimentKeySuffix,
            Status = ExperimentConfigStatus.Running,
            StickyCookieName = ExperimentRules.StickyCookiePrefix + service.ServiceKey,
            StickyDays = ExperimentRules.DefaultStickyDays,
        };
        Experiments.Add(experiment);

        foreach (var (key, weight, control) in variants)
        {
            var cluster = AddCluster($"http://10.0.1.{Variants.Count + 1}:8080", ownerServiceId: service.Id);
            Variants.Add(new YarpExperimentVariant
            {
                Id = NextId(),
                ExperimentId = experiment.Id,
                VariantKey = key,
                ClusterId = cluster.Id,
                WeightPercent = weight,
                IsControl = control,
            });
        }

        return experiment;
    }

    public YarpExperimentRule AddForceRule(YarpExperiment experiment, string type, string name, string? value = null, string? variantKey = null)
    {
        var rule = new YarpExperimentRule
        {
            Id = NextId(),
            ExperimentId = experiment.Id,
            SortOrder = Rules.Count(r => r.ExperimentId == experiment.Id),
            MatchType = type,
            MatchName = name,
            MatchValue = value,
            Action = ExperimentRuleTypes.ForceVariant,
            VariantId = variantKey is null ? null : Variants.Single(v => v.ExperimentId == experiment.Id && v.VariantKey == variantKey).Id,
        };
        Rules.Add(rule);
        return rule;
    }

    public YarpCluster ClusterOf(YarpExperiment experiment, string variantKey)
    {
        var variant = Variants.Single(v => v.ExperimentId == experiment.Id && v.VariantKey == variantKey);
        return Clusters.Single(c => c.Id == variant.ClusterId);
    }

    public ConfigSnapshot Build() => new(Services, Routes, Transforms, Clusters, Destinations, Experiments, Variants, Rules);
}
