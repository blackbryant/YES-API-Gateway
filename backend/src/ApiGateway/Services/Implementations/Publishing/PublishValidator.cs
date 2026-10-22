using System.Text.RegularExpressions;
using ApiGateway.Gateway;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using Microsoft.AspNetCore.Routing.Patterns;

namespace ApiGateway.Services.Implementations.Publishing;

/// <summary>
/// 發佈前驗證（FR-027、research R8）。純函式；收集全部錯誤後一次回傳，不在第一個錯誤就中止。
/// 只驗證會輸出到閘道的資料（啟用的服務與路由、它們引用的群組與實驗）。
/// </summary>
public static partial class PublishValidator
{
    private const string CatchAllMarker = "{**}";
    private const string ParameterMarker = "{}";

    public static IReadOnlyList<ValidationIssue> Validate(ConfigSnapshot snapshot, GatewayOptions options, DateTimeOffset now)
    {
        var issues = new List<ValidationIssue>();
        var services = snapshot.Services.Where(s => s.IsActive).ToDictionary(s => s.Id);
        var clusters = snapshot.Clusters.ToDictionary(c => c.Id);
        var experiments = snapshot.Experiments.ToDictionary(e => e.Id);
        var activeHostClusters = snapshot.Destinations.Where(d => d.IsActive).Select(d => d.ClusterId).ToHashSet();
        var routes = snapshot.Routes.Where(r => r.IsActive && services.ContainsKey(r.ServiceId)).ToList();
        var referencedExperiments = new HashSet<long>();

        foreach (var route in routes)
        {
            var service = services[route.ServiceId];
            ValidateRouteFields(route, service, options, issues);

            var (clusterId, experimentId) = YarpConfigBuilder.ResolveTarget(route, service);
            if (clusterId is { } cid)
            {
                ValidateCluster(cid, clusters, activeHostClusters, service, route.RouteKey, issues);
            }
            else if (experimentId is { } eid && experiments.ContainsKey(eid))
            {
                referencedExperiments.Add(eid);
            }
            else
            {
                issues.Add(Issue(PublishIssueCodes.RouteNoTarget, service, route.RouteKey));
            }
        }

        ValidateConflicts(routes, services, issues);

        foreach (var experimentId in referencedExperiments.Order())
        {
            var experiment = experiments[experimentId];
            var owner = services.GetValueOrDefault(experiment.ServiceId);
            ValidateExperiment(experiment, snapshot, clusters, activeHostClusters, owner, now, issues);
        }

        return issues;
    }

    private static void ValidateRouteFields(YarpRoute route, YarpService service, GatewayOptions options, List<ValidationIssue> issues)
    {
        if (!IsValidPath(route.MatchPath))
        {
            issues.Add(Issue(PublishIssueCodes.RoutePathInvalid, service, route.RouteKey, nameof(route.MatchPath), ("path", route.MatchPath)));
        }
        else
        {
            if (FindReservedPrefix(route.MatchPath, options.ReservedPathPrefixes) is { } reserved)
            {
                issues.Add(Issue(PublishIssueCodes.RouteReservedPath, service, route.RouteKey, nameof(route.MatchPath), ("path", route.MatchPath), ("reserved", reserved)));
            }

            if (route.MatchHosts.Length == 0 && IsRootCatchAll(route.MatchPath))
            {
                issues.Add(Issue(PublishIssueCodes.RouteRootCatchAllWithoutHost, service, route.RouteKey, nameof(route.MatchHosts)));
            }
        }

        foreach (var host in route.MatchHosts.Where(h => !IsValidHost(h)))
        {
            issues.Add(Issue(PublishIssueCodes.RouteHostInvalid, service, route.RouteKey, nameof(route.MatchHosts), ("host", host)));
        }

        foreach (var method in route.MatchMethods.Where(m => !RouteRules.AllowedMethods.Contains(m)))
        {
            issues.Add(Issue(PublishIssueCodes.RouteMethodInvalid, service, route.RouteKey, nameof(route.MatchMethods), ("method", method)));
        }

        if (route.Order is < RouteRules.MinOrder or > RouteRules.MaxOrder)
        {
            issues.Add(Issue(PublishIssueCodes.RouteOrderInvalid, service, route.RouteKey, nameof(route.Order), ("order", route.Order.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }
    }

    private static void ValidateCluster(
        long clusterId,
        IReadOnlyDictionary<long, YarpCluster> clusters,
        IReadOnlySet<long> activeHostClusters,
        YarpService? service,
        string? routeKey,
        List<ValidationIssue> issues,
        string? variantKey = null)
    {
        var parameters = variantKey is null ? [] : new[] { ("variant", variantKey) };
        if (!clusters.TryGetValue(clusterId, out var cluster) || !cluster.IsActive)
        {
            issues.Add(Issue(PublishIssueCodes.TargetClusterMissing, service, routeKey, null, parameters));
        }
        else if (!activeHostClusters.Contains(clusterId))
        {
            issues.Add(Issue(PublishIssueCodes.TargetClusterNoActiveHost, service, routeKey, null, [.. parameters, ("cluster", cluster.DisplayName)]));
        }
    }

    private static void ValidateConflicts(List<YarpRoute> routes, IReadOnlyDictionary<long, YarpService> services, List<ValidationIssue> issues)
    {
        var valid = routes.Where(r => IsValidPath(r.MatchPath)).ToList();
        for (var i = 0; i < valid.Count; i++)
        {
            for (var j = i + 1; j < valid.Count; j++)
            {
                if (Conflicts(valid[i], valid[j]))
                {
                    issues.Add(Issue(PublishIssueCodes.RouteConflict, services[valid[i].ServiceId], valid[i].RouteKey, nameof(YarpRoute.MatchPath), ("other", valid[j].RouteKey)));
                    issues.Add(Issue(PublishIssueCodes.RouteConflict, services[valid[j].ServiceId], valid[j].RouteKey, nameof(YarpRoute.MatchPath), ("other", valid[i].RouteKey)));
                }
            }
        }
    }

    /// <summary>正規化路徑相同、網域集合有交集（或皆空）、方法集合有交集（空 = 全部）、Order 相同</summary>
    private static bool Conflicts(YarpRoute a, YarpRoute b)
    {
        if (a.Order != b.Order || NormalizePath(a.MatchPath) != NormalizePath(b.MatchPath))
        {
            return false;
        }

        var hostsOverlap = (a.MatchHosts.Length == 0 && b.MatchHosts.Length == 0)
            || a.MatchHosts.Intersect(b.MatchHosts, StringComparer.OrdinalIgnoreCase).Any();
        var methodsOverlap = a.MatchMethods.Length == 0 || b.MatchMethods.Length == 0
            || a.MatchMethods.Intersect(b.MatchMethods, StringComparer.OrdinalIgnoreCase).Any();

        return hostsOverlap && methodsOverlap;
    }

    private static void ValidateExperiment(
        YarpExperiment experiment,
        ConfigSnapshot snapshot,
        IReadOnlyDictionary<long, YarpCluster> clusters,
        IReadOnlySet<long> activeHostClusters,
        YarpService? service,
        DateTimeOffset now,
        List<ValidationIssue> issues)
    {
        var variants = snapshot.Variants.Where(v => v.ExperimentId == experiment.Id).ToList();
        var experimentParam = ("experiment", experiment.ExperimentKey);

        if (variants.Count is < ExperimentRules.MinVariants or > ExperimentRules.MaxVariants)
        {
            issues.Add(Issue(PublishIssueCodes.ExperimentVariantCount, service, null, "variants", experimentParam, ("count", variants.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        if (variants.Count(v => v.IsControl) != 1)
        {
            issues.Add(Issue(PublishIssueCodes.ExperimentControl, service, null, "variants", experimentParam));
        }

        var sum = variants.Sum(v => v.WeightPercent);
        if (sum != ExperimentRules.MaxWeightPercent)
        {
            issues.Add(Issue(PublishIssueCodes.ExperimentWeightSum, service, null, "variants", experimentParam, ("sum", sum.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        foreach (var variant in variants)
        {
            ValidateCluster(variant.ClusterId, clusters, activeHostClusters, service, null, issues, variant.VariantKey);
        }

        if (experiment.StartAt is { } start && experiment.EndAt is { } end && end <= start)
        {
            issues.Add(Issue(PublishIssueCodes.ExperimentTimeRange, service, null, "endAt", experimentParam));
        }
        else if (experiment.Status == ExperimentConfigStatus.Running && experiment.EndAt is { } endAt && endAt <= now)
        {
            issues.Add(Issue(PublishIssueCodes.ExperimentEnded, service, null, "endAt", experimentParam));
        }

        if (!IsValidRollbackRule(experiment.AutoRollbackRule))
        {
            issues.Add(Issue(PublishIssueCodes.ExperimentRollbackRule, service, null, "autoRollback", experimentParam));
        }
    }

    public static bool IsValidRollbackRule(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            var rule = YarpConfigBuilder.ParseRollbackRule(json);
            return IsValidRollbackRule(rule);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public static bool IsValidRollbackRule(AutoRollbackDefinition? rule) =>
        rule is null
        || (rule.Metric == ExperimentRules.MetricFiveXxRate
            && rule.ThresholdPercent > 0 && rule.ThresholdPercent <= ExperimentRules.MaxWeightPercent
            && rule.DurationMin is >= ExperimentRules.MinDurationMin and <= ExperimentRules.MaxDurationMin
            && rule.MinSamples >= 1);

    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.Length > RouteRules.MatchPathMaxLength)
        {
            return false;
        }

        try
        {
            RoutePatternFactory.Parse(path);
            return true;
        }
        catch (RoutePatternException)
        {
            return false;
        }
    }

    public static bool IsValidHost(string? host) => !string.IsNullOrWhiteSpace(host) && HostRegex().IsMatch(host);

    /// <summary>MatchPath 第一個參數段之前的字面前綴，依路徑段（不分大小寫）比對保留前綴</summary>
    public static string? FindReservedPrefix(string path, IEnumerable<string> reservedPrefixes)
    {
        var literal = LiteralSegments(path);
        foreach (var reserved in reservedPrefixes)
        {
            var reservedSegments = reserved.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (reservedSegments.Length > 0
                && literal.Count >= reservedSegments.Length
                && reservedSegments.Select((s, i) => string.Equals(s, literal[i], StringComparison.OrdinalIgnoreCase)).All(x => x))
            {
                return reserved;
            }
        }

        return null;
    }

    public static bool IsRootCatchAll(string path) => NormalizePath(path) == "/" + CatchAllMarker;

    /// <summary>正規化：小寫、去尾端斜線、參數名稱抹除（{id} → {}，{**rest} → {**}）</summary>
    public static string NormalizePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.StartsWith('{')
                ? (s.StartsWith("{*", StringComparison.Ordinal) ? CatchAllMarker : ParameterMarker)
                : s.ToLowerInvariant());
        return "/" + string.Join('/', segments);
    }

    private static List<string> LiteralSegments(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries).TakeWhile(s => !s.Contains('{')).ToList();

    private static ValidationIssue Issue(string code, YarpService? service, string? routeKey, string? field = null, params (string Key, string Value)[] parameters) =>
        new(code, service?.Id, service?.DisplayName, routeKey, field, parameters.Length == 0 ? null : parameters.ToDictionary(p => p.Key, p => p.Value));

    [GeneratedRegex(RouteRules.HostPattern)]
    private static partial Regex HostRegex();
}
