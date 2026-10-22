using System.Text.Json;
using ApiGateway.Repositories.Interfaces;

namespace ApiGateway.Gateway;

/// <summary>
/// 啟動時載入生效版本到 DatabaseProxyConfigProvider。
/// 無生效版本或資料庫異常時只寫警告日誌，Gateway 以空設定（僅靜態路由）啟動（憲章一）。
/// 之後的版本變更由 ConfigSyncService 輪詢同步。
/// </summary>
public sealed class ProxyConfigLoader : IHostedService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseProxyConfigProvider _provider;
    private readonly ILogger<ProxyConfigLoader> _logger;

    public ProxyConfigLoader(IServiceScopeFactory scopeFactory, DatabaseProxyConfigProvider provider, ILogger<ProxyConfigLoader> logger)
    {
        _scopeFactory = scopeFactory;
        _provider = provider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var versions = scope.ServiceProvider.GetRequiredService<IConfigVersionRepository>();
            var active = await versions.GetActiveAsync(cancellationToken);

            if (active is null)
            {
                _logger.LogWarning("尚無生效的設定版本，閘道僅載入靜態路由");
                return;
            }

            var document = Deserialize(active.YarpConfig);
            _provider.ApplyDocument(document, active.VersionNo, active.Checksum);
            _logger.LogInformation("已載入設定版本 {VersionNo}（routes={RouteCount}, clusters={ClusterCount}, experiments={ExperimentCount}）",
                active.VersionNo, document.Routes.Count, document.Clusters.Count, document.Experiments.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "載入設定版本失敗，閘道僅載入靜態路由");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static YarpConfigDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<YarpConfigDocument>(json, SerializerOptions) ?? new YarpConfigDocument();
}
