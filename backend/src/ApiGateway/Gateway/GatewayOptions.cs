namespace ApiGateway.Gateway;

/// <summary>appsettings.json 的 Gateway 區段：閘道自身的靜態設定，不納入設定版本（憲章一）</summary>
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public HealthCheckOptions HealthCheck { get; set; } = new();

    /// <summary>系統保留路徑前綴；動態路由不得與其重疊</summary>
    public string[] ReservedPathPrefixes { get; set; } = [];

    /// <summary>節點識別；未設定時使用機器名稱</summary>
    public string? NodeId { get; set; }

    public string ResolveNodeId() => string.IsNullOrWhiteSpace(NodeId) ? Environment.MachineName : NodeId.Trim();
}

public sealed class HealthCheckOptions
{
    public const string DefaultPath = "/health";

    public string Path { get; set; } = DefaultPath;
}
