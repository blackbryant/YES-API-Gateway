using ApiGateway.Gateway;
using ApiGateway.Models.DTOs;
using ApiGateway.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ApiGateway.Services.Implementations;

/// <summary>靜態路由與系統保留路徑（appsettings.json，唯讀；US3 AS4）</summary>
public sealed class SystemRouteService : ISystemRouteService
{
    public const string ReverseProxyRoutesSection = "ReverseProxy:Routes";

    private readonly IAccessControlService _accessControl;
    private readonly GatewayOptions _options;
    private readonly IConfiguration _configuration;

    public SystemRouteService(IAccessControlService accessControl, IOptions<GatewayOptions> options, IConfiguration configuration)
    {
        _accessControl = accessControl;
        _options = options.Value;
        _configuration = configuration;
    }

    public async Task<SystemRoutesDto> GetAsync(long? operatorId, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);

        var staticRoutes = _configuration.GetSection(ReverseProxyRoutesSection).GetChildren()
            .Select(route => new StaticRouteDto(route.Key, route["Match:Path"], route["ClusterId"]))
            .OrderBy(route => route.RouteId, StringComparer.Ordinal)
            .ToList();

        return new SystemRoutesDto(_options.HealthCheck.Path, _options.ReservedPathPrefixes, staticRoutes);
    }
}
