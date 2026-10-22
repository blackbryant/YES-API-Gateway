using System.Collections.Concurrent;
using System.Text.Json;
using ApiGateway.Models.Constants;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ApiGateway.Gateway.Experiments;

/// <summary>
/// 自動回退監測（FR-019b ～ FR-019d、research R7）：每 10 秒評估執行中且設定回退條件的實驗。
/// 觸發時先以本機覆寫立即切回對照組，再條件式寫入執行期表與稽核；資料庫失敗時下一輪重試，
/// 本機覆寫仍持續生效（資料庫異常也不會把流量送往出錯的組別）。
/// </summary>
public sealed class AutoRollbackMonitor : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private const string AuditEntityType = "YarpExperimentRuntime";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseProxyConfigProvider _provider;
    private readonly ExperimentRuntimeCache _runtime;
    private readonly ExperimentMetricsCollector _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly string _nodeId;
    private readonly ILogger<AutoRollbackMonitor> _logger;
    private readonly ConcurrentDictionary<string, (string MetricJson, DateTimeOffset TriggeredAt)> _pendingPersist = new(StringComparer.Ordinal);

    public AutoRollbackMonitor(
        IServiceScopeFactory scopeFactory,
        DatabaseProxyConfigProvider provider,
        ExperimentRuntimeCache runtime,
        ExperimentMetricsCollector metrics,
        IOptions<GatewayOptions> options,
        TimeProvider timeProvider,
        ILogger<AutoRollbackMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _provider = provider;
        _runtime = runtime;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _nodeId = options.Value.ResolveNodeId();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(ExperimentRules.RollbackEvaluateIntervalSeconds), _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EvaluateOnceAsync(stoppingToken);
        }
    }

    /// <summary>評估一輪（供測試直接呼叫）</summary>
    public async Task EvaluateOnceAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var experiment in _provider.Experiments.All)
        {
            var definition = experiment.Definition;
            if (definition.AutoRollback is not { } rule
                || ExperimentStatusResolver.Resolve(definition, _runtime.Get(definition.Key)?.OverrideStatus, now) != ExperimentStatus.Running)
            {
                continue;
            }

            var window = TimeSpan.FromMinutes(rule.DurationMin);
            var stats = definition.Variants.Select(v =>
            {
                var s = _metrics.Snapshot(definition.Key, v.Key, window);
                return new VariantStats(v.Key, v.IsControl, s.Total, s.Errors);
            });

            if (AutoRollbackEvaluator.Evaluate(rule, stats) is { } decision)
            {
                _runtime.SetLocalRollback(definition.Key, now);
                var metric = JsonSerializer.Serialize(
                    new { variant = decision.VariantKey, rate = decision.RatePercent, samples = decision.Samples, windowMin = decision.WindowMin },
                    SerializerOptions);
                _pendingPersist[definition.Key] = (metric, now);
                _logger.LogWarning("A/B 實驗 {ExperimentKey} 觸發自動回退：{Variant} 組 5xx 比率 {Rate}%（{Samples} 筆）",
                    definition.Key, decision.VariantKey, decision.RatePercent, decision.Samples);
            }
        }

        await PersistPendingAsync(ct);
    }

    private async Task PersistPendingAsync(CancellationToken ct)
    {
        foreach (var (key, (metric, triggeredAt)) in _pendingPersist.ToArray())
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IExperimentRuntimeRepository>();
                if (await repository.TryMarkAutoRolledBackAsync(key, metric, _nodeId, triggeredAt, ct))
                {
                    scope.ServiceProvider.GetRequiredService<IAuditTrailService>()
                        .Record(AuditActions.ExperimentAutoRollback, AuditEntityType, key, null, after: JsonDocument.Parse(metric).RootElement);
                    await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
                }

                _pendingPersist.TryRemove(key, out _);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "自動回退狀態寫入失敗，下一輪重試（本節點已切回對照組）{ExperimentKey}", key);
            }
        }
    }
}
