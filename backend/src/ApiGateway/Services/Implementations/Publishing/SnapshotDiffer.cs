using System.Text.Json;
using System.Text.Json.Nodes;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;

namespace ApiGateway.Services.Implementations.Publishing;

/// <summary>
/// 草稿與生效版本的差異（FR-026）。純函式；比較時忽略 CreatedAt / UpdatedAt / UpdatedBy。
/// 轉換規則併入所屬路由、組別與分配規則併入所屬實驗。
/// </summary>
public static class SnapshotDiffer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] IgnoredProperties = ["createdAt", "updatedAt", "updatedBy"];

    public static IReadOnlyList<ChangeItem> Diff(ConfigSnapshot? active, ConfigSnapshot draft)
    {
        var before = Index(active ?? ConfigSnapshot.Empty);
        var after = Index(draft);
        var serviceNames = (active?.Services ?? []).Concat(draft.Services)
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.Last().DisplayName);

        var changes = new List<ChangeItem>();
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(k => k.Type).ThenBy(k => k.Key, StringComparer.Ordinal))
        {
            var hasBefore = before.TryGetValue(key, out var oldEntry);
            var hasAfter = after.TryGetValue(key, out var newEntry);
            var action = (hasBefore, hasAfter) switch
            {
                (false, true) => ChangeAction.Added,
                (true, false) => ChangeAction.Removed,
                _ when oldEntry!.Fingerprint != newEntry!.Fingerprint => ChangeAction.Modified,
                _ => (ChangeAction?)null,
            };

            if (action is null)
            {
                continue;
            }

            var serviceId = (newEntry ?? oldEntry)!.ServiceId;
            changes.Add(new ChangeItem(key.Type, key.Key, serviceId, serviceId is { } id ? serviceNames.GetValueOrDefault(id) : null, action.Value));
        }

        return changes;
    }

    private sealed record EntryKey(ChangeEntityType Type, string Key);

    private sealed record Entry(long? ServiceId, string Fingerprint);

    private static Dictionary<EntryKey, Entry> Index(ConfigSnapshot s)
    {
        var result = new Dictionary<EntryKey, Entry>();
        var clusterKeys = s.Clusters.ToDictionary(c => c.Id, c => c.ClusterKey);
        var clusterOwners = s.Clusters.ToDictionary(c => c.Id, c => c.OwnerServiceId);
        var transforms = s.RouteTransforms.ToLookup(t => t.RouteId);
        var variants = s.Variants.ToLookup(v => v.ExperimentId);
        var rules = s.ExperimentRules.ToLookup(r => r.ExperimentId);

        foreach (var service in s.Services)
        {
            result[new(ChangeEntityType.Service, service.ServiceKey)] = new(service.Id, Fingerprint(service));
        }

        foreach (var route in s.Routes)
        {
            result[new(ChangeEntityType.Route, route.RouteKey)] =
                new(route.ServiceId, Fingerprint(route) + Fingerprint(transforms[route.Id].OrderBy(t => t.Id).ToList()));
        }

        foreach (var cluster in s.Clusters)
        {
            result[new(ChangeEntityType.Cluster, cluster.ClusterKey)] = new(cluster.OwnerServiceId, Fingerprint(cluster));
        }

        foreach (var host in s.Destinations)
        {
            var clusterKey = clusterKeys.GetValueOrDefault(host.ClusterId, host.ClusterId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            result[new(ChangeEntityType.Host, $"{clusterKey}/{host.DestinationKey}")] =
                new(clusterOwners.GetValueOrDefault(host.ClusterId), Fingerprint(host));
        }

        foreach (var experiment in s.Experiments)
        {
            result[new(ChangeEntityType.Experiment, experiment.ExperimentKey)] = new(
                experiment.ServiceId,
                Fingerprint(experiment)
                + Fingerprint(variants[experiment.Id].OrderBy(v => v.Id).ToList())
                + Fingerprint(rules[experiment.Id].OrderBy(r => r.Id).ToList()));
        }

        return result;
    }

    private static string Fingerprint<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, SerializerOptions);
        Strip(node);
        return node?.ToJsonString() ?? string.Empty;
    }

    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in IgnoredProperties)
                {
                    obj.Remove(name);
                }

                foreach (var child in obj.Select(p => p.Value).ToList())
                {
                    Strip(child);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Strip(child);
                }

                break;
        }
    }
}
