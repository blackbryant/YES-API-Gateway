using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Gateway;

/// <summary>
/// 自訂 IProxyConfigProvider（憲章一）：保存目前生效版本的 YARP 設定，
/// Update 時以 IChangeToken 通知 YARP 熱更新，不需重啟、不中斷進行中的請求。
/// 請求熱路徑只讀取這裡的記憶體設定，不存取資料庫（憲章七）。
/// </summary>
public sealed class DatabaseProxyConfigProvider : IProxyConfigProvider
{
    private volatile GatewayProxyConfig _config = new([], [], null, null);

    public IProxyConfig GetConfig() => _config;

    /// <summary>目前載入的版本號（節點同步狀態比對用）</summary>
    public string? LoadedVersionNo => _config.VersionNo;

    public string? LoadedChecksum => _config.Checksum;

    public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters, string? versionNo, string? checksum)
    {
        var previous = _config;
        _config = new GatewayProxyConfig(routes, clusters, versionNo, checksum);
        previous.SignalChange();
    }

    private sealed class GatewayProxyConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _changeSource = new();

        public GatewayProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters, string? versionNo, string? checksum)
        {
            Routes = routes;
            Clusters = clusters;
            VersionNo = versionNo;
            Checksum = checksum;
            ChangeToken = new CancellationChangeToken(_changeSource.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }

        public string? VersionNo { get; }

        public string? Checksum { get; }

        public void SignalChange() => _changeSource.Cancel();
    }
}
