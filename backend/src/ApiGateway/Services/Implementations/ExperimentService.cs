using ApiGateway.Gateway;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;

namespace ApiGateway.Services.Implementations;

/// <summary>
/// A/B 實驗的執行期查詢與手動操作（FR-019e、FR-021）。
/// 手動暫停 / 恢復 / 結束只寫入執行期表與稽核，不產生設定版本、不標記草稿（憲章一）。
/// </summary>
public sealed class ExperimentService : IExperimentService
{
    public const string AttentionTypeAutoRolledBack = "experimentAutoRolledBack";
    public const string SeverityCritical = "critical";
    public const string ServicesPathPrefix = "/services/";
    private const string AuditEntityType = "YarpExperimentRuntime";

    private static readonly ExperimentStatus[] ListedStatuses = [ExperimentStatus.Scheduled, ExperimentStatus.Running, ExperimentStatus.Paused];

    private readonly IAccessControlService _accessControl;
    private readonly IExperimentStateReader _states;
    private readonly IExperimentGatewayControl _gateway;
    private readonly IExperimentRuntimeRepository _runtime;
    private readonly IServiceRepository _services;
    private readonly IAuditTrailService _auditTrail;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ExperimentService(
        IAccessControlService accessControl,
        IExperimentStateReader states,
        IExperimentGatewayControl gateway,
        IExperimentRuntimeRepository runtime,
        IServiceRepository services,
        IAuditTrailService auditTrail,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _accessControl = accessControl;
        _states = states;
        _gateway = gateway;
        _runtime = runtime;
        _services = services;
        _auditTrail = auditTrail;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ExperimentSummaryDto>> GetSummaryAsync(long? operatorId, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);

        var listed = _states.PublishedExperiments
            .Select(d => (Definition: d, State: _states.Get(d.Key)))
            .Where(x => ListedStatuses.Contains(x.State.Status))
            .ToList();
        var owners = await OwnersAsync(listed.Select(x => x.Definition.Key).ToList(), ct);

        return listed
            .Where(x => owners.ContainsKey(x.Definition.Key))
            .Select(x => ToSummary(x.Definition, x.State, owners[x.Definition.Key]))
            .ToList();
    }

    public async Task<IReadOnlyList<AttentionItemDto>> GetAttentionItemsAsync(long? operatorId, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);

        var rolledBack = _states.PublishedExperiments
            .Select(d => (Definition: d, State: _states.Get(d.Key)))
            .Where(x => x.State.IsAutoRolledBack)
            .ToList();
        var owners = await OwnersAsync(rolledBack.Select(x => x.Definition.Key).ToList(), ct);

        return rolledBack
            .Where(x => owners.ContainsKey(x.Definition.Key))
            .Select(x =>
            {
                var owner = owners[x.Definition.Key];
                return new AttentionItemDto(
                    $"experiment-{x.Definition.Key}",
                    AttentionTypeAutoRolledBack,
                    SeverityCritical,
                    new Dictionary<string, string> { ["service"] = owner.ServiceName, ["experiment"] = x.Definition.Key },
                    ServicesPathPrefix + owner.ServiceId,
                    x.State.TriggeredAt ?? _timeProvider.GetUtcNow());
            })
            .ToList();
    }

    public async Task<ExperimentSummaryDto?> RunActionAsync(long? operatorId, string experimentKey, ExperimentRuntimeAction action, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);

        var state = _states.Get(experimentKey);
        if (state.Published is null || state.Status == ExperimentStatus.Draft)
        {
            return null;
        }

        var allowed = action switch
        {
            ExperimentRuntimeAction.Pause => state.Status is ExperimentStatus.Running or ExperimentStatus.Scheduled,
            ExperimentRuntimeAction.Resume => state.Status == ExperimentStatus.Paused,
            ExperimentRuntimeAction.End => state.Status is not ExperimentStatus.Ended,
            _ => false,
        };
        if (!allowed)
        {
            throw new ArgumentException(ErrorCodes.ExperimentsActionInvalid);
        }

        var now = _timeProvider.GetUtcNow();
        var row = await _runtime.GetForUpdateAsync(experimentKey, ct);
        if (row is null)
        {
            row = new YarpExperimentRuntime { ExperimentKey = experimentKey };
            _runtime.Add(row);
        }

        var before = new { row.OverrideStatus, row.IsAutoRolledBack };
        row.OverrideStatus = action switch
        {
            ExperimentRuntimeAction.Pause => ExperimentOverrideStatus.Paused,
            ExperimentRuntimeAction.End => ExperimentOverrideStatus.Ended,
            _ => null,
        };
        row.IsAutoRolledBack = false;
        row.UpdatedBy = admin.Id;
        row.UpdatedAt = now;
        if (action == ExperimentRuntimeAction.Resume)
        {
            row.TriggeredMetric = null;
            row.TriggeredAt = null;
            row.TriggeredByNode = null;
        }

        _auditTrail.Record(AuditActionOf(action), AuditEntityType, experimentKey, admin.Id, before, new { row.OverrideStatus, row.IsAutoRolledBack });
        await _unitOfWork.SaveChangesAsync(ct);

        // 本節點立即生效；其他節點於下一輪同步（≤ 5 秒）套用
        _gateway.ApplyOverride(experimentKey, row.OverrideStatus is null ? null : new ExperimentRuntimeOverride(row.OverrideStatus, false, null));
        if (action == ExperimentRuntimeAction.Resume)
        {
            _gateway.ResetMetrics(experimentKey);
        }

        var owners = await OwnersAsync([experimentKey], ct);
        return owners.TryGetValue(experimentKey, out var owner) ? ToSummary(state.Published, _states.Get(experimentKey), owner) : null;
    }

    private async Task<Dictionary<string, ExperimentOwner>> OwnersAsync(IReadOnlyCollection<string> keys, CancellationToken ct) =>
        keys.Count == 0
            ? []
            : (await _services.GetExperimentOwnersAsync(keys, ct)).ToDictionary(o => o.ExperimentKey, StringComparer.Ordinal);

    private static ExperimentSummaryDto ToSummary(ExperimentDefinition definition, ExperimentState state, ExperimentOwner owner) => new(
        definition.Key,
        owner.ServiceId,
        owner.ServiceName,
        state.Status,
        state.IsAutoRolledBack,
        state.TriggeredAt,
        definition.StartAt,
        definition.EndAt,
        definition.Variants.Select(v => new ExperimentVariantSummaryDto(v.Key, v.Weight, v.IsControl)).ToList(),
        // 主機即時健康狀態屬 F-08，尚無資料時視為正常
        HasUnhealthyHost: false);

    private static string AuditActionOf(ExperimentRuntimeAction action) => action switch
    {
        ExperimentRuntimeAction.Pause => AuditActions.ExperimentPause,
        ExperimentRuntimeAction.Resume => AuditActions.ExperimentResume,
        _ => AuditActions.ExperimentEnd,
    };
}
