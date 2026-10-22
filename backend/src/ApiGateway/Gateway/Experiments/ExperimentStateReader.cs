using ApiGateway.Models.Constants;

namespace ApiGateway.Gateway.Experiments;

/// <summary>實驗目前的有效狀態（依生效版本、執行期覆寫與現在時間）</summary>
public sealed record ExperimentState(
    ExperimentStatus Status,
    bool IsAutoRolledBack,
    DateTimeOffset? TriggeredAt,
    ExperimentDefinition? Published);

/// <summary>供 Admin API 讀取閘道記憶體中的實驗狀態（唯讀）</summary>
public interface IExperimentStateReader
{
    ExperimentState Get(string experimentKey);

    IEnumerable<ExperimentDefinition> PublishedExperiments { get; }
}

/// <summary>手動操作後同步本節點的閘道狀態（其他節點由 ConfigSyncService 輪詢套用）</summary>
public interface IExperimentGatewayControl
{
    void ApplyOverride(string experimentKey, ExperimentRuntimeOverride? runtimeOverride);

    /// <summary>清除計數（恢復後重新監測回退條件）</summary>
    void ResetMetrics(string experimentKey);
}

public sealed class ExperimentStateReader : IExperimentStateReader, IExperimentGatewayControl
{
    private readonly DatabaseProxyConfigProvider _provider;
    private readonly ExperimentRuntimeCache _runtime;
    private readonly ExperimentMetricsCollector _metrics;
    private readonly TimeProvider _timeProvider;

    public ExperimentStateReader(DatabaseProxyConfigProvider provider, ExperimentRuntimeCache runtime, ExperimentMetricsCollector metrics, TimeProvider timeProvider)
    {
        _provider = provider;
        _runtime = runtime;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public IEnumerable<ExperimentDefinition> PublishedExperiments => _provider.Experiments.All.Select(x => x.Definition);

    public ExperimentState Get(string experimentKey)
    {
        var published = _provider.Experiments.TryGet(experimentKey, out var definition) ? definition.Definition : null;
        var runtime = _runtime.Get(experimentKey);
        var status = ExperimentStatusResolver.Resolve(published, runtime?.OverrideStatus, _timeProvider.GetUtcNow());
        return new ExperimentState(status, status == ExperimentStatus.Paused && runtime?.IsAutoRolledBack == true, runtime?.TriggeredAt, published);
    }

    public void ApplyOverride(string experimentKey, ExperimentRuntimeOverride? runtimeOverride) => _runtime.SetStored(experimentKey, runtimeOverride);

    public void ResetMetrics(string experimentKey) => _metrics.Reset(experimentKey);
}
