namespace ApiGateway.Models.DTOs;

/// <summary>閘道節點同步狀態（對應前端 GatewayNode）</summary>
public sealed record GatewayNodeDto(string Name, string? LoadedVersion, bool Synced, bool Online, DateTimeOffset LastHeartbeatAt);

/// <summary>靜態路由與系統保留路徑（唯讀）</summary>
public sealed record SystemRoutesDto(string HealthCheckPath, IReadOnlyList<string> ReservedPathPrefixes, IReadOnlyList<StaticRouteDto> StaticRoutes);

public sealed record StaticRouteDto(string RouteId, string? Path, string? ClusterId);
