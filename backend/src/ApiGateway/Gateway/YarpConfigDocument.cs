using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Gateway;

/// <summary>YarpConfigVersions.YarpConfig 的 JSON 結構（由發佈流程產生，閘道直接載入）</summary>
public sealed class YarpConfigDocument
{
    public List<RouteConfig> Routes { get; set; } = [];

    public List<ClusterConfig> Clusters { get; set; } = [];

    /// <summary>A/B 實驗定義（路由以 Metadata["Yes.Experiment"] 引用）</summary>
    public List<ExperimentDefinition> Experiments { get; set; } = [];
}

public sealed record ExperimentDefinition
{
    public string Key { get; init; } = string.Empty;

    /// <summary>設定狀態（Draft / Running）</summary>
    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? StartAt { get; init; }

    public DateTimeOffset? EndAt { get; init; }

    public StickyDefinition Sticky { get; init; } = new();

    public List<VariantDefinition> Variants { get; init; } = [];

    public List<ForceRuleDefinition> ForceRules { get; init; } = [];

    public AutoRollbackDefinition? AutoRollback { get; init; }
}

public sealed record StickyDefinition
{
    public string CookieName { get; init; } = string.Empty;

    public int Days { get; init; }
}

public sealed record VariantDefinition
{
    public string Key { get; init; } = string.Empty;

    /// <summary>群組的 ClusterKey（ULID）</summary>
    public string ClusterId { get; init; } = string.Empty;

    public int Weight { get; init; }

    public bool IsControl { get; init; }
}

public sealed record ForceRuleDefinition
{
    /// <summary>Header / Cookie / Query</summary>
    public string Type { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>空值：以請求帶入的值作為組別代號</summary>
    public string? Value { get; init; }

    /// <summary>Value 符合時強制進入的組別</summary>
    public string? Variant { get; init; }
}

public sealed record AutoRollbackDefinition
{
    public string Metric { get; init; } = Models.Constants.ExperimentRules.MetricFiveXxRate;

    public decimal ThresholdPercent { get; init; }

    public int DurationMin { get; init; }

    public int MinSamples { get; init; }
}
