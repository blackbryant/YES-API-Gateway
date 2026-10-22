using ApiGateway.Models.DTOs;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;

namespace ApiGateway.Services.Implementations;

/// <summary>頂部列草稿狀態：生效版本 + 未發佈變更數</summary>
public sealed class DraftService : IDraftService
{
    private readonly IAccessControlService _accessControl;
    private readonly IConfigDraftRepository _drafts;
    private readonly IConfigVersionRepository _versions;

    public DraftService(IAccessControlService accessControl, IConfigDraftRepository drafts, IConfigVersionRepository versions)
    {
        _accessControl = accessControl;
        _drafts = drafts;
        _versions = versions;
    }

    public async Task<DraftStatusDto> GetStatusAsync(long? operatorId, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);

        var draft = await _drafts.GetAsync(ct);
        var active = await _versions.GetActiveAsync(ct);

        return new DraftStatusDto(active?.VersionNo, draft is { IsDirty: true } ? draft.ChangeCount : 0);
    }
}
