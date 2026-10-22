using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;

namespace ApiGateway.Services.Implementations;

/// <summary>閘道節點同步狀態：節點校驗碼等於生效版本即為已同步（FR-032）</summary>
public sealed class GatewayNodeService : IGatewayNodeService
{
    private readonly IAccessControlService _accessControl;
    private readonly IGatewayNodeRepository _nodes;
    private readonly IConfigVersionRepository _versions;
    private readonly TimeProvider _timeProvider;

    public GatewayNodeService(IAccessControlService accessControl, IGatewayNodeRepository nodes, IConfigVersionRepository versions, TimeProvider timeProvider)
    {
        _accessControl = accessControl;
        _nodes = nodes;
        _versions = versions;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<GatewayNodeDto>> ListAsync(long? operatorId, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);

        var active = await _versions.GetActiveChecksumAsync(ct);
        var staleBefore = _timeProvider.GetUtcNow().AddSeconds(-GatewaySyncRules.NodeStaleSeconds);

        return (await _nodes.ListAsync(ct))
            .Select(n => new GatewayNodeDto(
                n.NodeId,
                n.LoadedVersionNo?.Trim(),
                active is { } a && n.LoadedChecksum == a.Checksum,
                n.LastHeartbeatAt >= staleBefore,
                n.LastHeartbeatAt))
            .ToList();
    }
}
