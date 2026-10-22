using System.Text.Json;
using ApiGateway.Models.Entities;

namespace ApiGateway.Services.Implementations.Publishing;

/// <summary>
/// 所有納入設定版本的資料表內容（發佈時存入 YarpConfigVersions.Snapshot，供差異比對與 F-13 回滾）。
/// 各清單依 Id 排序，序列化結果具確定性。
/// </summary>
public sealed record ConfigSnapshot(
    IReadOnlyList<YarpService> Services,
    IReadOnlyList<YarpRoute> Routes,
    IReadOnlyList<YarpRouteTransform> RouteTransforms,
    IReadOnlyList<YarpCluster> Clusters,
    IReadOnlyList<YarpDestination> Destinations,
    IReadOnlyList<YarpExperiment> Experiments,
    IReadOnlyList<YarpExperimentVariant> Variants,
    IReadOnlyList<YarpExperimentRule> ExperimentRules)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ConfigSnapshot Empty { get; } = new([], [], [], [], [], [], [], []);

    public string Serialize() => JsonSerializer.Serialize(Normalize(), SerializerOptions);

    /// <summary>反序列化；缺少的清單視為空（相容其他來源產生的快照）</summary>
    public static ConfigSnapshot Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        var raw = JsonSerializer.Deserialize<RawSnapshot>(json, SerializerOptions);
        return raw is null
            ? Empty
            : new ConfigSnapshot(
                raw.Services ?? [],
                raw.Routes ?? [],
                raw.RouteTransforms ?? [],
                raw.Clusters ?? [],
                raw.Destinations ?? [],
                raw.Experiments ?? [],
                raw.Variants ?? [],
                raw.ExperimentRules ?? []);
    }

    private ConfigSnapshot Normalize() => new(
        Services.OrderBy(x => x.Id).ToList(),
        Routes.OrderBy(x => x.Id).ToList(),
        RouteTransforms.OrderBy(x => x.Id).ToList(),
        Clusters.OrderBy(x => x.Id).ToList(),
        Destinations.OrderBy(x => x.Id).ToList(),
        Experiments.OrderBy(x => x.Id).ToList(),
        Variants.OrderBy(x => x.Id).ToList(),
        ExperimentRules.OrderBy(x => x.Id).ToList());

    private sealed class RawSnapshot
    {
        public List<YarpService>? Services { get; set; }
        public List<YarpRoute>? Routes { get; set; }
        public List<YarpRouteTransform>? RouteTransforms { get; set; }
        public List<YarpCluster>? Clusters { get; set; }
        public List<YarpDestination>? Destinations { get; set; }
        public List<YarpExperiment>? Experiments { get; set; }
        public List<YarpExperimentVariant>? Variants { get; set; }
        public List<YarpExperimentRule>? ExperimentRules { get; set; }
    }
}
