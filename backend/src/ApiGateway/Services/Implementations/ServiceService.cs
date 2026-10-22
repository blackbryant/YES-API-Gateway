using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Infrastructure.Data;
using ApiGateway.Infrastructure.Ids;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Models.Responses;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Services.Implementations;

/// <summary>
/// 服務維護（US1、US4、US5，research R9）：一次送出服務、入口、主機或 A/B 設定，於單一交易同步各表。
/// 所有異動只寫入草稿（標記草稿 + 稽核），發佈後才生效（憲章一）。
/// </summary>
public sealed partial class ServiceService : IServiceService
{
    private const string AuditEntityType = "YarpServices";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAccessControlService _accessControl;
    private readonly IServiceRepository _services;
    private readonly IConfigDraftRepository _drafts;
    private readonly IAuditTrailService _auditTrail;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemIdGenerator _idGenerator;
    private readonly IServiceReadSupport _readSupport;
    private readonly ILogger<ServiceService> _logger;

    public ServiceService(
        IAccessControlService accessControl,
        IServiceRepository services,
        IConfigDraftRepository drafts,
        IAuditTrailService auditTrail,
        IUnitOfWork unitOfWork,
        ISystemIdGenerator idGenerator,
        IServiceReadSupport readSupport,
        ILogger<ServiceService> logger)
    {
        _accessControl = accessControl;
        _services = services;
        _drafts = drafts;
        _auditTrail = auditTrail;
        _unitOfWork = unitOfWork;
        _idGenerator = idGenerator;
        _readSupport = readSupport;
        _logger = logger;
    }

    // ---------------------------------------------------------------- 查詢

    public async Task<PagedResponse<ServiceSummaryDto>> SearchAsync(long? operatorId, ServiceQuery query, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);
        if (query.PageIndex < 1 || query.PageSize < 1 || query.PageSize > PagingRules.MaxPageSize)
        {
            throw new ArgumentException(ErrorCodes.PagingInvalid);
        }

        var (rows, total) = await _services.SearchAsync(query.Keyword, query.Tag, (query.PageIndex - 1) * query.PageSize, query.PageSize, ct);
        var publishedAt = await _readSupport.GetActivePublishedAtAsync(ct);

        var items = rows.Select(r => new ServiceSummaryDto(
            r.Service.Id,
            r.Service.ServiceKey,
            r.Service.DisplayName,
            r.Service.Tags,
            r.Service.IsActive,
            r.RouteCount,
            r.Hosts,
            r.Service.DefaultExperimentId is null ? RoutingMode.Single : RoutingMode.AbTest,
            publishedAt is null || r.Service.UpdatedAt > publishedAt,
            r.Service.UpdatedAt)).ToList();

        return new PagedResponse<ServiceSummaryDto>(items, query.PageIndex, query.PageSize, total);
    }

    public async Task<ServiceDetailDto?> GetAsync(long? operatorId, long id, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);

        var aggregate = await _services.GetAggregateAsync(id, tracking: false, ct);
        return aggregate is null ? null : ToDetail(aggregate);
    }

    // ---------------------------------------------------------------- 寫入

    public async Task<ServiceDetailDto> CreateAsync(long? operatorId, ServiceSaveRequest request, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);
        var model = Validate(request, existingServiceKey: null);

        if (await _services.ExistsByKeyAsync(model.ServiceKey, null, ct))
        {
            throw new InvalidOperationException(ErrorCodes.ServicesKeyExists);
        }

        var service = new YarpService { ServiceKey = model.ServiceKey };
        var aggregate = new ServiceAggregate(service, [], [], [], [], null, [], []);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        ApplyServiceFields(service, model, admin.Id);
        _services.Add(service);
        await SaveAsync(ct);

        await SyncAsync(aggregate, model, admin.Id, ct);

        var result = ToDetail(aggregate);
        await _drafts.MarkDirtyAsync(admin.Id, _readSupport.Now, ct);
        _auditTrail.Record(AuditActions.Create, AuditEntityType, service.ServiceKey, admin.Id, after: result);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("服務已新增 {ServiceKey} by {UserId}", service.ServiceKey, admin.Id);
        return result;
    }

    public async Task<ServiceDetailDto?> UpdateAsync(long? operatorId, long id, ServiceSaveRequest request, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);

        var aggregate = await _services.GetAggregateAsync(id, tracking: true, ct);
        if (aggregate is null)
        {
            return null;
        }

        var model = Validate(request, aggregate.Service.ServiceKey);
        var before = ToDetail(aggregate);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        ApplyServiceFields(aggregate.Service, model, admin.Id);
        await SyncAsync(aggregate, model, admin.Id, ct);

        var after = ToDetail(aggregate);
        await _drafts.MarkDirtyAsync(admin.Id, _readSupport.Now, ct);
        _auditTrail.Record(AuditActions.Update, AuditEntityType, aggregate.Service.ServiceKey, admin.Id, before, after);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("服務已修改 {ServiceKey} by {UserId}", aggregate.Service.ServiceKey, admin.Id);
        return after;
    }

    public async Task<bool> SetActiveAsync(long? operatorId, long id, bool isActive, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);

        var aggregate = await _services.GetAggregateAsync(id, tracking: true, ct);
        if (aggregate is null)
        {
            return false;
        }

        var before = new { aggregate.Service.IsActive };
        aggregate.Service.IsActive = isActive;
        aggregate.Service.UpdatedBy = admin.Id;
        await _drafts.MarkDirtyAsync(admin.Id, _readSupport.Now, ct);
        _auditTrail.Record(AuditActions.Update, AuditEntityType, aggregate.Service.ServiceKey, admin.Id, before, new { IsActive = isActive });
        await SaveAsync(ct);

        _logger.LogInformation("服務已{Action} {ServiceKey} by {UserId}", isActive ? "啟用" : "停用", aggregate.Service.ServiceKey, admin.Id);
        return true;
    }

    public async Task<bool> DeleteAsync(long? operatorId, long id, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);

        var aggregate = await _services.GetAggregateAsync(id, tracking: true, ct);
        if (aggregate is null)
        {
            return false;
        }

        var owned = aggregate.Clusters.Where(c => c.OwnerServiceId == id).ToList();
        if (await _services.IsClusterReferencedOutsideAsync(owned.Select(c => c.Id).ToList(), id, ct))
        {
            throw new InvalidOperationException(ErrorCodes.ClustersInUse);
        }

        var before = ToDetail(aggregate);
        var service = aggregate.Service;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        // 1. 解除服務對群組 / 實驗的引用，刪除入口（轉換規則隨資料庫串聯刪除）與實驗（組別、規則串聯刪除）
        service.DefaultClusterId = null;
        service.DefaultExperimentId = null;
        aggregate.Transforms.ForEach(_services.Remove);
        aggregate.Routes.ForEach(_services.Remove);
        aggregate.Rules.ForEach(_services.Remove);
        aggregate.Variants.ForEach(_services.Remove);
        if (aggregate.Experiment is not null)
        {
            _services.Remove(aggregate.Experiment);
        }

        await SaveAsync(ct);

        // 2. 刪除系統為此服務建立的群組與主機
        var ownedIds = owned.Select(c => c.Id).ToHashSet();
        aggregate.Destinations.Where(d => ownedIds.Contains(d.ClusterId)).ToList().ForEach(_services.Remove);
        await SaveAsync(ct);
        owned.ForEach(_services.Remove);
        _services.Remove(service);

        await _drafts.MarkDirtyAsync(admin.Id, _readSupport.Now, ct);
        _auditTrail.Record(AuditActions.Delete, AuditEntityType, service.ServiceKey, admin.Id, before: before);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("服務已刪除 {ServiceKey} by {UserId}", service.ServiceKey, admin.Id);
        return true;
    }

    // ---------------------------------------------------------------- 同步

    /// <summary>依請求同步入口、轉換、群組 / 主機與實驗（呼叫前服務本身已有 Id）</summary>
    private async Task SyncAsync(ServiceAggregate aggregate, ServiceModel model, long operatorId, CancellationToken ct)
    {
        var service = aggregate.Service;
        var obsoleteClusters = new List<YarpCluster>();
        YarpExperiment? obsoleteExperiment = null;

        // ---- 群組與實驗（新群組需先取得 Id）----
        if (model.RoutingMode == RoutingMode.Single)
        {
            var main = FindMainCluster(aggregate) ?? NewCluster(aggregate, service.DisplayName, operatorId);
            main.DisplayName = service.DisplayName;
            main.UpdatedBy = operatorId;

            if (aggregate.Experiment is not null)
            {
                obsoleteExperiment = aggregate.Experiment;
                obsoleteClusters.AddRange(aggregate.Variants.Select(v => Cluster(aggregate, v.ClusterId)).Where(c => c != main && c.OwnerServiceId == service.Id));
            }

            await SaveAsync(ct);
            service.DefaultClusterId = main.Id;
            service.DefaultExperimentId = null;
            SyncHosts(aggregate, main, model.Hosts);
        }
        else
        {
            var experimentModel = model.Experiment!;
            var experiment = aggregate.Experiment;
            if (experiment is null)
            {
                experiment = new YarpExperiment
                {
                    ServiceId = service.Id,
                    ExperimentKey = service.ServiceKey + ExperimentRules.ExperimentKeySuffix,
                };
                _services.Add(experiment);
                aggregate.Experiment = experiment;
            }

            ApplyExperimentFields(experiment, experimentModel, service.ServiceKey, operatorId);

            // 組別群組：既有組別沿用；控制組可沿用原本單一模式的群組；其餘新建
            var previousMain = service.DefaultExperimentId is null ? FindMainCluster(aggregate) : null;
            var variantClusters = new Dictionary<string, YarpCluster>(StringComparer.Ordinal);
            foreach (var variant in experimentModel.Variants)
            {
                var existing = aggregate.Variants.FirstOrDefault(v => v.VariantKey == variant.VariantKey);
                YarpCluster cluster;
                if (existing is not null)
                {
                    cluster = Cluster(aggregate, existing.ClusterId);
                }
                else if (variant.IsControl && previousMain is not null)
                {
                    cluster = previousMain;
                    previousMain = null;
                }
                else
                {
                    cluster = NewCluster(aggregate, string.Empty, operatorId);
                }

                cluster.DisplayName = $"{service.DisplayName} · {variant.VariantKey}";
                cluster.UpdatedBy = operatorId;
                variantClusters[variant.VariantKey] = cluster;
            }

            if (previousMain is not null && previousMain.OwnerServiceId == service.Id)
            {
                obsoleteClusters.Add(previousMain);
            }

            await SaveAsync(ct);

            // 組別列
            foreach (var removed in aggregate.Variants.Where(v => experimentModel.Variants.All(m => m.VariantKey != v.VariantKey)).ToList())
            {
                var cluster = Cluster(aggregate, removed.ClusterId);
                if (cluster.OwnerServiceId == service.Id)
                {
                    obsoleteClusters.Add(cluster);
                }

                aggregate.Variants.Remove(removed);
                _services.Remove(removed);
            }

            aggregate.Rules.ForEach(_services.Remove);
            aggregate.Rules.Clear();

            foreach (var variantModel in experimentModel.Variants)
            {
                var cluster = variantClusters[variantModel.VariantKey];
                var variant = aggregate.Variants.FirstOrDefault(v => v.VariantKey == variantModel.VariantKey);
                if (variant is null)
                {
                    variant = new YarpExperimentVariant { ExperimentId = experiment.Id, VariantKey = variantModel.VariantKey };
                    _services.Add(variant);
                    aggregate.Variants.Add(variant);
                }

                variant.ClusterId = cluster.Id;
                variant.WeightPercent = (short)variantModel.WeightPercent;
                variant.IsControl = variantModel.IsControl;
                variant.UpdatedBy = operatorId;
                SyncHosts(aggregate, cluster, variantModel.Hosts);
            }

            service.DefaultExperimentId = experiment.Id;
            service.DefaultClusterId = null;
            await SaveAsync(ct);

            // 強制分組規則（需組別 Id）
            for (var i = 0; i < experimentModel.ForceRules.Count; i++)
            {
                var rule = experimentModel.ForceRules[i];
                var entity = new YarpExperimentRule
                {
                    ExperimentId = experiment.Id,
                    SortOrder = i,
                    MatchType = rule.MatchType,
                    MatchName = rule.MatchName,
                    MatchValue = rule.MatchValue,
                    Action = ExperimentRuleTypes.ForceVariant,
                    VariantId = rule.VariantKey is null ? null : aggregate.Variants.Single(v => v.VariantKey == rule.VariantKey).Id,
                    UpdatedBy = operatorId,
                };
                _services.Add(entity);
                aggregate.Rules.Add(entity);
            }
        }

        // ---- 入口與轉換 ----
        var routePairs = SyncRoutes(aggregate, model, operatorId);
        await SaveAsync(ct);
        SyncTransforms(aggregate, routePairs, operatorId);

        // ---- 刪除不再使用的實驗與群組（先刪引用者再刪群組）----
        if (obsoleteExperiment is not null)
        {
            aggregate.Rules.ForEach(_services.Remove);
            aggregate.Variants.ForEach(_services.Remove);
            _services.Remove(obsoleteExperiment);
            aggregate.Rules.Clear();
            aggregate.Variants.Clear();
            aggregate.Experiment = null;
        }

        if (obsoleteClusters.Count > 0)
        {
            var ids = obsoleteClusters.Select(c => c.Id).ToHashSet();
            if (await _services.IsClusterReferencedOutsideAsync(ids, service.Id, ct))
            {
                throw new InvalidOperationException(ErrorCodes.ClustersInUse);
            }

            await SaveAsync(ct);
            foreach (var destination in aggregate.Destinations.Where(d => ids.Contains(d.ClusterId)).ToList())
            {
                aggregate.Destinations.Remove(destination);
                _services.Remove(destination);
            }

            await SaveAsync(ct);
            foreach (var cluster in obsoleteClusters)
            {
                aggregate.Clusters.Remove(cluster);
                _services.Remove(cluster);
            }
        }

        await SaveAsync(ct);
    }

    private List<(RouteModel Model, YarpRoute Route)> SyncRoutes(ServiceAggregate aggregate, ServiceModel model, long operatorId)
    {
        var service = aggregate.Service;
        // 序號以刪除前的最大值計算，避免同一次儲存重複使用剛刪除的 RouteKey（唯一索引）
        var seq = aggregate.Routes.Select(r => RouteSequence(r.RouteKey, service.ServiceKey)).DefaultIfEmpty(0).Max();
        var keep = model.Routes.Where(r => r.Id is not null).Select(r => r.Id!.Value).ToHashSet();
        foreach (var removed in aggregate.Routes.Where(r => !keep.Contains(r.Id)).ToList())
        {
            foreach (var transform in aggregate.Transforms.Where(t => t.RouteId == removed.Id).ToList())
            {
                aggregate.Transforms.Remove(transform);
                _services.Remove(transform);
            }

            aggregate.Routes.Remove(removed);
            _services.Remove(removed);
        }

        var pairs = new List<(RouteModel, YarpRoute)>();
        foreach (var routeModel in model.Routes)
        {
            var route = routeModel.Id is { } id ? aggregate.Routes.Single(r => r.Id == id) : null;
            if (route is null)
            {
                route = new YarpRoute { ServiceId = service.Id, RouteKey = $"{service.ServiceKey}-{++seq}" };
                _services.Add(route);
                aggregate.Routes.Add(route);
            }

            route.MatchPath = routeModel.MatchPath;
            route.MatchHosts = routeModel.MatchHosts;
            route.MatchMethods = routeModel.MatchMethods;
            route.Order = routeModel.Order;
            route.IsActive = routeModel.IsActive;
            route.Description = routeModel.Description;
            route.UpdatedBy = operatorId;
            pairs.Add((routeModel, route));
        }

        return pairs;
    }

    private void SyncTransforms(ServiceAggregate aggregate, List<(RouteModel Model, YarpRoute Route)> pairs, long operatorId)
    {
        foreach (var (routeModel, route) in pairs)
        {
            var existing = aggregate.Transforms.FirstOrDefault(t => t.RouteId == route.Id && t.TransformType == RouteRules.TransformPathRemovePrefix);

            if (routeModel.PathRemovePrefix is null)
            {
                if (existing is not null)
                {
                    aggregate.Transforms.Remove(existing);
                    _services.Remove(existing);
                }

                continue;
            }

            if (existing is null)
            {
                existing = new YarpRouteTransform
                {
                    RouteId = route.Id,
                    Direction = RouteRules.TransformDirectionRequest,
                    TransformType = RouteRules.TransformPathRemovePrefix,
                };
                _services.Add(existing);
                aggregate.Transforms.Add(existing);
            }

            existing.SortOrder = 0;
            existing.Definition = JsonSerializer.Serialize(new Dictionary<string, string> { [RouteRules.TransformPathRemovePrefix] = routeModel.PathRemovePrefix });
            existing.UpdatedBy = operatorId;
        }
    }

    private void SyncHosts(ServiceAggregate aggregate, YarpCluster cluster, IReadOnlyList<HostModel> hosts)
    {
        var current = aggregate.Destinations.Where(d => d.ClusterId == cluster.Id).ToList();
        var keep = hosts.Where(h => h.Id is not null).Select(h => h.Id!.Value).ToHashSet();
        foreach (var removed in current.Where(d => !keep.Contains(d.Id)))
        {
            aggregate.Destinations.Remove(removed);
            _services.Remove(removed);
        }

        foreach (var host in hosts)
        {
            var destination = host.Id is { } id ? current.FirstOrDefault(d => d.Id == id) : null;
            if (destination is null)
            {
                if (host.Id is not null)
                {
                    throw new ArgumentException(ErrorCodes.HostsNotFound);
                }

                destination = new YarpDestination
                {
                    ClusterId = cluster.Id,
                    DestinationKey = RouteRules.DestinationKeyPrefix + _idGenerator.NewUlid(),
                };
                _services.Add(destination);
                aggregate.Destinations.Add(destination);
            }

            destination.Address = host.Address;
            destination.IsActive = host.IsActive;
        }
    }

    private YarpCluster NewCluster(ServiceAggregate aggregate, string displayName, long operatorId)
    {
        var cluster = new YarpCluster
        {
            ClusterKey = _idGenerator.NewUlid(),
            DisplayName = displayName,
            OwnerServiceId = aggregate.Service.Id,
            LoadBalancingPolicy = ClusterRules.DefaultLoadBalancingPolicy,
            HttpVersion = ClusterRules.DefaultHttpVersion,
            HttpVersionPolicy = ClusterRules.DefaultHttpVersionPolicy,
            ActivityTimeoutSec = ClusterRules.DefaultActivityTimeoutSec,
            IsActive = true,
            UpdatedBy = operatorId,
        };
        _services.Add(cluster);
        aggregate.Clusters.Add(cluster);
        return cluster;
    }

    /// <summary>單一模式的群組：服務的預設群組；A/B 模式下則為對照組群組</summary>
    private static YarpCluster? FindMainCluster(ServiceAggregate aggregate)
    {
        if (aggregate.Service.DefaultClusterId is { } id)
        {
            return aggregate.Clusters.FirstOrDefault(c => c.Id == id);
        }

        var control = aggregate.Variants.FirstOrDefault(v => v.IsControl);
        return control is null ? null : aggregate.Clusters.FirstOrDefault(c => c.Id == control.ClusterId);
    }

    private static YarpCluster Cluster(ServiceAggregate aggregate, long id) => aggregate.Clusters.Single(c => c.Id == id);

    private static int RouteSequence(string routeKey, string serviceKey) =>
        routeKey.StartsWith(serviceKey + "-", StringComparison.Ordinal)
        && int.TryParse(routeKey.AsSpan(serviceKey.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var seq)
            ? seq
            : 0;

    private static void ApplyServiceFields(YarpService service, ServiceModel model, long operatorId)
    {
        service.DisplayName = model.DisplayName;
        service.Tags = model.Tags;
        service.IsActive = model.IsActive;
        service.Description = model.Description;
        service.UpdatedBy = operatorId;
    }

    private static void ApplyExperimentFields(YarpExperiment experiment, ExperimentModel model, string serviceKey, long operatorId)
    {
        experiment.Status = model.Enabled ? ExperimentConfigStatus.Running : ExperimentConfigStatus.Draft;
        experiment.StickyCookieName = model.StickyCookieName ?? ExperimentRules.StickyCookiePrefix + serviceKey;
        experiment.StickyDays = model.StickyDays;
        experiment.StartAt = model.StartAt;
        experiment.EndAt = model.EndAt;
        experiment.AutoRollbackRule = model.AutoRollback is null ? null : JsonSerializer.Serialize(model.AutoRollback, SerializerOptions);
        experiment.UpdatedBy = operatorId;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new InvalidOperationException(ErrorCodes.ServicesKeyExists, ex);
        }
    }

    // ---------------------------------------------------------------- DTO

    private ServiceDetailDto ToDetail(ServiceAggregate aggregate)
    {
        var service = aggregate.Service;
        var experiment = aggregate.Experiment;
        var mode = service.DefaultExperimentId is not null ? RoutingMode.AbTest : RoutingMode.Single;
        var hostsByCluster = aggregate.Destinations.ToLookup(d => d.ClusterId);
        var mainCluster = mode == RoutingMode.Single ? FindMainCluster(aggregate) : null;

        var routes = aggregate.Routes
            .OrderBy(r => r.Order)
            .ThenBy(r => r.Id)
            .Select(r => new RouteDetailDto(
                r.Id,
                r.RouteKey,
                r.MatchPath,
                r.MatchHosts,
                r.MatchMethods,
                r.Order,
                ReadPrefix(aggregate.Transforms.FirstOrDefault(t => t.RouteId == r.Id && t.TransformType == RouteRules.TransformPathRemovePrefix)),
                r.IsActive,
                r.Description))
            .ToList();

        ExperimentDetailDto? experimentDto = null;
        if (mode == RoutingMode.AbTest && experiment is not null)
        {
            var state = _readSupport.GetExperimentState(experiment.ExperimentKey);
            var variantKeys = aggregate.Variants.ToDictionary(v => v.Id, v => v.VariantKey);
            var rollback = YarpConfigBuilder.ParseRollbackRule(experiment.AutoRollbackRule);
            experimentDto = new ExperimentDetailDto(
                experiment.ExperimentKey,
                experiment.Status == ExperimentConfigStatus.Running,
                experiment.StickyCookieName,
                experiment.StickyDays,
                experiment.StartAt,
                experiment.EndAt,
                rollback is null ? null : new AutoRollbackRuleDto(rollback.Metric, rollback.ThresholdPercent, rollback.DurationMin, rollback.MinSamples),
                aggregate.Variants
                    .OrderBy(v => v.VariantKey, StringComparer.Ordinal)
                    .Select(v =>
                    {
                        var cluster = aggregate.Clusters.FirstOrDefault(c => c.Id == v.ClusterId);
                        return new VariantDetailDto(
                            v.VariantKey,
                            v.WeightPercent,
                            v.IsControl,
                            new ClusterInfoDto(cluster?.ClusterKey ?? string.Empty, cluster?.DisplayName ?? string.Empty),
                            hostsByCluster[v.ClusterId].Select(ToHostDto).ToList());
                    })
                    .ToList(),
                aggregate.Rules
                    .OrderBy(r => r.SortOrder)
                    .Select(r => new ForceRuleInput(r.MatchType, r.MatchName, r.MatchValue, r.VariantId is { } vid ? variantKeys.GetValueOrDefault(vid) : null))
                    .ToList(),
                state.Status,
                state.IsAutoRolledBack);
        }

        return new ServiceDetailDto(
            service.Id,
            service.ServiceKey,
            service.DisplayName,
            service.Tags,
            service.IsActive,
            service.Description,
            routes,
            mode,
            mainCluster is null ? null : new ClusterInfoDto(mainCluster.ClusterKey, mainCluster.DisplayName),
            mainCluster is null ? [] : hostsByCluster[mainCluster.Id].Select(ToHostDto).ToList(),
            experimentDto,
            service.UpdatedAt);
    }

    private static HostDto ToHostDto(YarpDestination d) => new(d.Id, d.Address, d.IsActive);

    private static string? ReadPrefix(YarpRouteTransform? transform) =>
        transform is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(transform.Definition)?.GetValueOrDefault(RouteRules.TransformPathRemovePrefix);

    // ---------------------------------------------------------------- 驗證

    private sealed record ServiceModel(
        string ServiceKey,
        string DisplayName,
        string[] Tags,
        bool IsActive,
        string? Description,
        IReadOnlyList<RouteModel> Routes,
        RoutingMode RoutingMode,
        IReadOnlyList<HostModel> Hosts,
        ExperimentModel? Experiment);

    private sealed record RouteModel(long? Id, string MatchPath, string[] MatchHosts, string[] MatchMethods, int Order, string? PathRemovePrefix, bool IsActive, string? Description);

    private sealed record HostModel(long? Id, string Address, bool IsActive);

    private sealed record VariantModel(string VariantKey, int WeightPercent, bool IsControl, IReadOnlyList<HostModel> Hosts);

    private sealed record ForceRuleModel(string MatchType, string MatchName, string? MatchValue, string? VariantKey);

    private sealed record ExperimentModel(
        bool Enabled,
        string? StickyCookieName,
        int StickyDays,
        DateTimeOffset? StartAt,
        DateTimeOffset? EndAt,
        Gateway.AutoRollbackDefinition? AutoRollback,
        IReadOnlyList<VariantModel> Variants,
        IReadOnlyList<ForceRuleModel> ForceRules);

    private static ServiceModel Validate(ServiceSaveRequest request, string? existingServiceKey)
    {
        var serviceKey = request.ServiceKey?.Trim() ?? string.Empty;
        if (!ServiceKeyRegex().IsMatch(serviceKey))
        {
            throw new ArgumentException(ErrorCodes.ServicesKeyInvalid);
        }

        if (existingServiceKey is not null && serviceKey != existingServiceKey)
        {
            throw new ArgumentException(ErrorCodes.ServicesKeyImmutable);
        }

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0 || displayName.Length > RouteRules.DisplayNameMaxLength)
        {
            throw new ArgumentException(ErrorCodes.ServicesDisplayNameInvalid);
        }

        var tags = (request.Tags ?? []).Select(t => t?.Trim() ?? string.Empty).Distinct(StringComparer.Ordinal).ToArray();
        if (tags.Length > RouteRules.MaxTags || tags.Any(t => t.Length == 0 || t.Length > RouteRules.TagMaxLength))
        {
            throw new ArgumentException(ErrorCodes.ServicesTagsInvalid);
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description?.Length > RouteRules.DescriptionMaxLength)
        {
            throw new ArgumentException(ErrorCodes.ServicesDescriptionTooLong);
        }

        if (request.Routes is not { Count: > 0 })
        {
            throw new ArgumentException(ErrorCodes.ServicesRoutesRequired);
        }

        var routes = request.Routes.Select(ValidateRoute).ToList();
        if (routes.Where(r => r.Id is not null).GroupBy(r => r.Id).Any(g => g.Count() > 1))
        {
            throw new ArgumentException(ErrorCodes.RoutesNotFound);
        }

        return request.RoutingMode switch
        {
            RoutingMode.Single => new ServiceModel(serviceKey, displayName, tags, request.IsActive, description, routes, RoutingMode.Single,
                ValidateHosts(request.Hosts), null),
            RoutingMode.AbTest when request.Experiment is not null => new ServiceModel(serviceKey, displayName, tags, request.IsActive, description, routes,
                RoutingMode.AbTest, [], ValidateExperiment(request.Experiment)),
            _ => throw new ArgumentException(ErrorCodes.ServicesRoutingModeInvalid),
        };
    }

    private static RouteModel ValidateRoute(RouteInput input)
    {
        var path = input.MatchPath?.Trim() ?? string.Empty;
        if (!PublishValidator.IsValidPath(path))
        {
            throw new ArgumentException(ErrorCodes.RoutesPathInvalid);
        }

        var hosts = (input.MatchHosts ?? []).Select(h => h?.Trim().ToLowerInvariant() ?? string.Empty).Distinct(StringComparer.Ordinal).ToArray();
        if (hosts.Length > RouteRules.MaxHosts || hosts.Any(h => !PublishValidator.IsValidHost(h)))
        {
            throw new ArgumentException(ErrorCodes.RoutesHostInvalid);
        }

        var methods = (input.MatchMethods ?? []).Select(m => m?.Trim().ToUpperInvariant() ?? string.Empty).Distinct(StringComparer.Ordinal).ToArray();
        if (methods.Any(m => !RouteRules.AllowedMethods.Contains(m)))
        {
            throw new ArgumentException(ErrorCodes.RoutesMethodInvalid);
        }

        var order = input.Order ?? RouteRules.DefaultOrder;
        if (order is < RouteRules.MinOrder or > RouteRules.MaxOrder)
        {
            throw new ArgumentException(ErrorCodes.RoutesOrderInvalid);
        }

        var prefix = string.IsNullOrWhiteSpace(input.PathRemovePrefix) ? null : input.PathRemovePrefix.Trim().TrimEnd('/');
        if (prefix is not null && !IsLiteralPrefixOf(prefix, path))
        {
            throw new ArgumentException(ErrorCodes.RoutesPrefixInvalid);
        }

        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description?.Length > RouteRules.DescriptionMaxLength)
        {
            throw new ArgumentException(ErrorCodes.ServicesDescriptionTooLong);
        }

        return new RouteModel(input.Id, path, hosts, methods, order, prefix, input.IsActive, description);
    }

    /// <summary>前綴以 / 開頭、不含萬用字元，且依路徑段為 MatchPath 字面部分的前綴</summary>
    private static bool IsLiteralPrefixOf(string prefix, string path)
    {
        if (prefix.Length == 0 || prefix[0] != '/' || prefix.Contains('{') || prefix.Contains('*'))
        {
            return false;
        }

        var prefixSegments = prefix.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var literal = path.Split('/', StringSplitOptions.RemoveEmptyEntries).TakeWhile(s => !s.Contains('{')).ToList();
        return prefixSegments.Length > 0
            && prefixSegments.Length <= literal.Count
            && prefixSegments.Select((s, i) => string.Equals(s, literal[i], StringComparison.OrdinalIgnoreCase)).All(x => x);
    }

    private static IReadOnlyList<HostModel> ValidateHosts(List<HostInput>? hosts)
    {
        if (hosts is not { Count: > 0 })
        {
            throw new ArgumentException(ErrorCodes.ServicesHostsRequired);
        }

        return hosts.Select(h =>
        {
            var address = h.Address?.Trim().TrimEnd('/') ?? string.Empty;
            if (address.Length > RouteRules.AddressMaxLength
                || !Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException(ErrorCodes.HostsAddressInvalid);
            }

            return new HostModel(h.Id, address, h.IsActive);
        }).ToList();
    }

    private static ExperimentModel ValidateExperiment(ExperimentInput input)
    {
        var variants = (input.Variants ?? [])
            .Select(v => new VariantModel(v.VariantKey?.Trim().ToUpperInvariant() ?? string.Empty, v.WeightPercent, v.IsControl, ValidateHosts(v.Hosts)))
            .ToList();

        var valid = variants.Count is >= ExperimentRules.MinVariants and <= ExperimentRules.MaxVariants
            && variants.All(v => VariantKeyRegex().IsMatch(v.VariantKey) && v.WeightPercent is >= 0 and <= ExperimentRules.MaxWeightPercent)
            && variants.Select(v => v.VariantKey).Distinct(StringComparer.Ordinal).Count() == variants.Count
            && variants.Sum(v => v.WeightPercent) == ExperimentRules.MaxWeightPercent
            && variants.Count(v => v.IsControl) == 1
            && input.StickyDays is >= ExperimentRules.MinStickyDays and <= ExperimentRules.MaxStickyDays
            && (input.StickyCookieName is null || CookieNameRegex().IsMatch(input.StickyCookieName))
            && (input.StartAt is null || input.EndAt is null || input.EndAt > input.StartAt);

        var rules = (input.ForceRules ?? [])
            .Select(r => new ForceRuleModel(
                r.MatchType?.Trim() ?? string.Empty,
                r.MatchName?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(r.MatchValue) ? null : r.MatchValue.Trim(),
                string.IsNullOrWhiteSpace(r.VariantKey) ? null : r.VariantKey.Trim().ToUpperInvariant()))
            .ToList();
        valid = valid && rules.All(r =>
            ExperimentRuleTypes.MatchTypes.Contains(r.MatchType)
            && r.MatchName.Length is > 0 and <= ExperimentRules.RuleNameMaxLength
            && (r.MatchValue?.Length ?? 0) <= ExperimentRules.RuleValueMaxLength
            && (r.MatchValue is null) == (r.VariantKey is null)
            && (r.VariantKey is null || variants.Any(v => v.VariantKey == r.VariantKey)));

        Gateway.AutoRollbackDefinition? rollback = null;
        if (input.AutoRollback is { } rule)
        {
            rollback = new Gateway.AutoRollbackDefinition
            {
                Metric = rule.Metric ?? ExperimentRules.MetricFiveXxRate,
                ThresholdPercent = rule.ThresholdPercent ?? ExperimentRules.DefaultThresholdPercent,
                DurationMin = rule.DurationMin ?? ExperimentRules.DefaultDurationMin,
                MinSamples = rule.MinSamples ?? ExperimentRules.DefaultMinSamples,
            };
            valid = valid && PublishValidator.IsValidRollbackRule(rollback);
        }

        if (!valid)
        {
            throw new ArgumentException(ErrorCodes.ExperimentsInvalid);
        }

        return new ExperimentModel(input.Enabled, input.StickyCookieName?.Trim(), input.StickyDays, input.StartAt, input.EndAt, rollback, variants, rules);
    }

    [GeneratedRegex(RouteRules.ServiceKeyPattern)]
    private static partial Regex ServiceKeyRegex();

    [GeneratedRegex(ExperimentRules.VariantKeyPattern)]
    private static partial Regex VariantKeyRegex();

    [GeneratedRegex(ExperimentRules.CookieNamePattern)]
    private static partial Regex CookieNameRegex();
}
