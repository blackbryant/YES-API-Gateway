using System.Collections.Frozen;

namespace ApiGateway.Gateway.Experiments;

/// <summary>
/// 生效版本中的 A/B 實驗定義（記憶體、不可變）。與 Routes / Clusters 一起原子替換，
/// 閘道熱路徑只讀取這裡，不存取資料庫（憲章七）。
/// </summary>
public sealed class ExperimentTable
{
    public static ExperimentTable Empty { get; } = new(FrozenDictionary<string, ExperimentRuntimeDefinition>.Empty);

    private readonly FrozenDictionary<string, ExperimentRuntimeDefinition> _experiments;

    private ExperimentTable(FrozenDictionary<string, ExperimentRuntimeDefinition> experiments)
    {
        _experiments = experiments;
    }

    public static ExperimentTable From(IEnumerable<ExperimentDefinition> definitions) =>
        new(definitions.ToFrozenDictionary(d => d.Key, d => new ExperimentRuntimeDefinition(d), StringComparer.Ordinal));

    public bool TryGet(string key, out ExperimentRuntimeDefinition definition) => _experiments.TryGetValue(key, out definition!);

    public IEnumerable<ExperimentRuntimeDefinition> All => _experiments.Values;
}

/// <summary>實驗定義 + 預先計算的累積權重</summary>
public sealed class ExperimentRuntimeDefinition
{
    public ExperimentRuntimeDefinition(ExperimentDefinition definition)
    {
        Definition = definition;
        Variants = definition.Variants.ToFrozenDictionary(v => v.Key, StringComparer.Ordinal);
        Control = definition.Variants.FirstOrDefault(v => v.IsControl) ?? definition.Variants.FirstOrDefault() ?? new VariantDefinition();

        var cumulative = 0;
        var weights = new List<(int, VariantDefinition)>();
        foreach (var variant in definition.Variants.Where(v => v.Weight > 0))
        {
            cumulative += variant.Weight;
            weights.Add((cumulative, variant));
        }

        CumulativeWeights = [.. weights];
    }

    public ExperimentDefinition Definition { get; }

    public string Key => Definition.Key;

    public FrozenDictionary<string, VariantDefinition> Variants { get; }

    public VariantDefinition Control { get; }

    /// <summary>(累積權重上界, 組別)；抽籤值 r ∈ [0,100) 時取第一個 r &lt; 上界 的組別</summary>
    public (int UpperBound, VariantDefinition Variant)[] CumulativeWeights { get; }
}
