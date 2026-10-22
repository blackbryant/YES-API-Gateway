using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiGateway.Gateway;
using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace ApiGateway.Services.Implementations.Publishing;

/// <summary>
/// 服務 → YARP 設定轉換（憲章二、research R4）。純函式、不存取資料庫，相同輸入產生相同輸出。
/// 導向解析：路由的群組 / 實驗優先，皆空時沿用服務（路由值 ?? 服務值）。
/// </summary>
public static class YarpConfigBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static YarpConfigDocument Build(ConfigSnapshot snapshot)
    {
        var clustersById = snapshot.Clusters.ToDictionary(x => x.Id);
        var experimentsById = snapshot.Experiments.ToDictionary(x => x.Id);
        var transformsByRoute = snapshot.RouteTransforms.ToLookup(x => x.RouteId);
        var activeServices = snapshot.Services.Where(x => x.IsActive).ToDictionary(x => x.Id);

        var routes = new List<RouteConfig>();
        var usedExperiments = new HashSet<long>();

        foreach (var route in snapshot.Routes.Where(r => r.IsActive && activeServices.ContainsKey(r.ServiceId)).OrderBy(r => r.RouteKey, StringComparer.Ordinal))
        {
            var target = ResolveTarget(route, activeServices[route.ServiceId]);
            string? clusterKey = null;
            Dictionary<string, string>? metadata = null;

            if (target.ClusterId is { } clusterId && clustersById.TryGetValue(clusterId, out var cluster))
            {
                clusterKey = cluster.ClusterKey;
            }
            else if (target.ExperimentId is { } experimentId && experimentsById.ContainsKey(experimentId))
            {
                var control = snapshot.Variants.FirstOrDefault(v => v.ExperimentId == experimentId && v.IsControl);
                if (control is not null && clustersById.TryGetValue(control.ClusterId, out var controlCluster))
                {
                    clusterKey = controlCluster.ClusterKey;
                }

                metadata = new Dictionary<string, string> { [ExperimentRules.RouteMetadataKey] = experimentsById[experimentId].ExperimentKey };
                usedExperiments.Add(experimentId);
            }

            routes.Add(new RouteConfig
            {
                RouteId = route.RouteKey,
                ClusterId = clusterKey,
                Order = route.Order,
                Match = new RouteMatch
                {
                    Path = route.MatchPath,
                    Hosts = route.MatchHosts.Length == 0 ? null : route.MatchHosts,
                    Methods = route.MatchMethods.Length == 0 ? null : route.MatchMethods,
                },
                Transforms = BuildTransforms(transformsByRoute[route.Id]),
                Metadata = metadata,
            });
        }

        var destinationsByCluster = snapshot.Destinations.Where(d => d.IsActive).ToLookup(d => d.ClusterId);
        var clusters = snapshot.Clusters
            .Where(c => c.IsActive)
            .OrderBy(c => c.ClusterKey, StringComparer.Ordinal)
            .Select(c => BuildCluster(c, destinationsByCluster[c.Id]))
            .ToList();

        var experiments = snapshot.Experiments
            .Where(e => usedExperiments.Contains(e.Id))
            .OrderBy(e => e.ExperimentKey, StringComparer.Ordinal)
            .Select(e => BuildExperiment(e, snapshot, clustersById))
            .ToList();

        return new YarpConfigDocument { Routes = routes, Clusters = clusters, Experiments = experiments };
    }

    /// <summary>YarpConfig 正規化 JSON 的 SHA-256（小寫 hex）</summary>
    public static string ComputeChecksum(YarpConfigDocument document)
    {
        var json = JsonSerializer.Serialize(document, SerializerOptions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public static string Serialize(YarpConfigDocument document) => JsonSerializer.Serialize(document, SerializerOptions);

    /// <summary>路由的有效導向：路由的群組或實驗優先，兩者皆空時沿用服務</summary>
    public static (long? ClusterId, long? ExperimentId) ResolveTarget(YarpRoute route, YarpService service)
    {
        if (route.ClusterId is not null || route.ExperimentId is not null)
        {
            return (route.ClusterId, route.ExperimentId);
        }

        return (service.DefaultClusterId, service.DefaultClusterId is null ? service.DefaultExperimentId : null);
    }

    private static List<IReadOnlyDictionary<string, string>>? BuildTransforms(IEnumerable<YarpRouteTransform> transforms)
    {
        var list = transforms
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Id)
            .Select(t => JsonSerializer.Deserialize<Dictionary<string, string>>(t.Definition, SerializerOptions))
            .Where(d => d is { Count: > 0 })
            .Select(d => (IReadOnlyDictionary<string, string>)d!)
            .ToList();

        return list.Count == 0 ? null : list;
    }

    private static ClusterConfig BuildCluster(YarpCluster cluster, IEnumerable<YarpDestination> destinations) => new()
    {
        ClusterId = cluster.ClusterKey,
        LoadBalancingPolicy = cluster.LoadBalancingPolicy,
        HttpRequest = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromSeconds(cluster.ActivityTimeoutSec),
            Version = Version.TryParse(cluster.HttpVersion, out var version) ? version : HttpVersion.Version20,
            VersionPolicy = Enum.TryParse<HttpVersionPolicy>(cluster.HttpVersionPolicy, out var policy) ? policy : HttpVersionPolicy.RequestVersionOrLower,
        },
        HttpClient = new HttpClientConfig
        {
            MaxConnectionsPerServer = cluster.MaxConnectionsPerServer,
            DangerousAcceptAnyServerCertificate = cluster.AcceptAnyServerCertificate,
        },
        Destinations = destinations
            .OrderBy(d => d.DestinationKey, StringComparer.Ordinal)
            .ToDictionary(
                d => d.DestinationKey,
                d => new DestinationConfig { Address = d.Address, Health = d.HealthAddress, Host = d.Host },
                StringComparer.OrdinalIgnoreCase),
    };

    private static ExperimentDefinition BuildExperiment(YarpExperiment experiment, ConfigSnapshot snapshot, IReadOnlyDictionary<long, YarpCluster> clustersById)
    {
        var variants = snapshot.Variants
            .Where(v => v.ExperimentId == experiment.Id)
            .OrderBy(v => v.VariantKey, StringComparer.Ordinal)
            .ToList();
        var variantKeys = variants.ToDictionary(v => v.Id, v => v.VariantKey);

        return new ExperimentDefinition
        {
            Key = experiment.ExperimentKey,
            Status = experiment.Status,
            StartAt = experiment.StartAt,
            EndAt = experiment.EndAt,
            Sticky = new StickyDefinition { CookieName = experiment.StickyCookieName, Days = experiment.StickyDays },
            Variants = variants
                .Select(v => new VariantDefinition
                {
                    Key = v.VariantKey,
                    ClusterId = clustersById.TryGetValue(v.ClusterId, out var c) ? c.ClusterKey : string.Empty,
                    Weight = v.WeightPercent,
                    IsControl = v.IsControl,
                })
                .ToList(),
            ForceRules = snapshot.ExperimentRules
                .Where(r => r.ExperimentId == experiment.Id && r.Action == ExperimentRuleTypes.ForceVariant)
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.Id)
                .Select(r => new ForceRuleDefinition
                {
                    Type = r.MatchType,
                    Name = r.MatchName ?? string.Empty,
                    Value = r.MatchValue,
                    Variant = r.VariantId is { } id && variantKeys.TryGetValue(id, out var key) ? key : null,
                })
                .ToList(),
            AutoRollback = ParseRollbackRule(experiment.AutoRollbackRule),
        };
    }

    public static AutoRollbackDefinition? ParseRollbackRule(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<AutoRollbackDefinition>(json, SerializerOptions);
}
