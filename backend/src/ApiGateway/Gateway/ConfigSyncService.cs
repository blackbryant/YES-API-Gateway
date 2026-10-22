using ApiGateway.Gateway.Experiments;
using ApiGateway.Models.Constants;
using ApiGateway.Repositories.Interfaces;
using Microsoft.Extensions.Options;

namespace ApiGateway.Gateway;

/// <summary>
/// 多節點同步（FR-032、research R3）：每 5 秒比對生效版本 Checksum，不同時載入新版；
/// 同時更新實驗執行期覆寫與節點心跳。資料庫異常只記警告，閘道維持記憶體中的最後一版（憲章一）。
/// 熱路徑不經此處，請求只讀記憶體設定（憲章七）。
/// </summary>
public sealed class ConfigSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGatewayConfigApplier _applier;
    private readonly ExperimentRuntimeCache _runtimeCache;
    private readonly TimeProvider _timeProvider;
    private readonly string _nodeId;
    private readonly ILogger<ConfigSyncService> _logger;

    public ConfigSyncService(
        IServiceScopeFactory scopeFactory,
        IGatewayConfigApplier applier,
        ExperimentRuntimeCache runtimeCache,
        IOptions<GatewayOptions> options,
        TimeProvider timeProvider,
        ILogger<ConfigSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _applier = applier;
        _runtimeCache = runtimeCache;
        _timeProvider = timeProvider;
        _nodeId = options.Value.ResolveNodeId();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(GatewaySyncRules.PollIntervalSeconds), _timeProvider);
        do
        {
            await SyncOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>執行一輪同步（供測試直接呼叫）</summary>
    public async Task SyncOnceAsync(CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var versions = scope.ServiceProvider.GetRequiredService<IConfigVersionRepository>();

            var active = await versions.GetActiveChecksumAsync(ct);
            if (active is { } current && current.Checksum != _applier.LoadedChecksum)
            {
                var version = await versions.GetActiveAsync(ct);
                if (version is not null)
                {
                    _applier.ApplyDocument(ProxyConfigLoader.Deserialize(version.YarpConfig), version.VersionNo, version.Checksum);
                    _logger.LogInformation("節點 {NodeId} 已同步到設定版本 {VersionNo}", _nodeId, version.VersionNo);
                }
            }

            var runtime = scope.ServiceProvider.GetRequiredService<IExperimentRuntimeRepository>();
            _runtimeCache.ReplaceFromStorage(await runtime.ListAsync(ct));

            var nodes = scope.ServiceProvider.GetRequiredService<IGatewayNodeRepository>();
            await nodes.UpsertHeartbeatAsync(_nodeId, _applier.LoadedVersionNo, _applier.LoadedChecksum, _timeProvider.GetUtcNow(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "設定同步失敗，閘道維持目前載入的版本 {VersionNo}", _applier.LoadedVersionNo);
        }
    }
}
