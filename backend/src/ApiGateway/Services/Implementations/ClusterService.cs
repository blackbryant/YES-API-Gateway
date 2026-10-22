
using ApiGateway.Infrastructure.Data;
using ApiGateway.Infrastructure.Ids;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Models.Responses;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Services.Implementations;

/// <summary>
/// 主機群組維護（範例模組，新模組請比照）。
/// 所有異動只寫入草稿（標記草稿 dirty + 稽核），要「發佈」後才會進入新版本生效（憲章一）。
/// </summary>
public sealed partial class ClusterService : IClusterService
{
    private const string AuditEntityType = "YarpClusters";

    private readonly IAccessControlService _accessControl;
    private readonly IClusterRepository _clusters;
    private readonly IConfigDraftRepository _drafts;
    private readonly IAuditTrailService _auditTrail;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ClusterService> _logger;

    public ClusterService(
        IAccessControlService accessControl,
        IClusterRepository clusters,
        IConfigDraftRepository drafts,
        IAuditTrailService auditTrail,
        IUnitOfWork unitOfWork,
        ISystemIdGenerator idGenerator,
        TimeProvider timeProvider,
        ILogger<ClusterService> logger)
    {
        _accessControl = accessControl;
        _clusters = clusters;
        _drafts = drafts;
        _auditTrail = auditTrail;
        _unitOfWork = unitOfWork;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PagedResponse<ClusterDto>> SearchAsync(long? operatorId, ClusterQuery query, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);
        ValidatePaging(query.PageIndex, query.PageSize);

        var skip = (query.PageIndex - 1) * query.PageSize;
        var (items, total) = await _clusters.SearchAsync(query.Keyword, skip, query.PageSize, ct);

        return new PagedResponse<ClusterDto>(items.Select(ToDto).ToList(), query.PageIndex, query.PageSize, total);
    }

    public async Task<ClusterDto?> GetAsync(long? operatorId, long id, CancellationToken ct = default)
    {
        await _accessControl.EnsureSignedInAsync(operatorId, ct);

        var cluster = await _clusters.GetByIdAsync(id, ct);
        return cluster is null ? null : ToDto(cluster);
    }

    public async Task<ClusterDto> CreateAsync(long? operatorId, ClusterSaveRequest request, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);
        ValidateSave(request);

        // ClusterId 由系統產生 ULID（FR-02-05）
        var key = _idGenerator.NewUlid();
        var cluster = new YarpCluster { ClusterKey = key };
        Apply(cluster, request, admin.Id);
        _clusters.Add(cluster);

        await _drafts.MarkDirtyAsync(admin.Id, _timeProvider.GetUtcNow(), ct);
        _auditTrail.Record(AuditActions.Create, AuditEntityType, key, admin.Id, after: ToDto(cluster));
        await SaveAsync(ct);

        _logger.LogInformation("主機群組已新增 {ClusterKey} by {UserId}", key, admin.Id);
        return ToDto(cluster);
    }

    public async Task<ClusterDto?> UpdateAsync(long? operatorId, long id, ClusterSaveRequest request, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);
        ValidateSave(request);

        var cluster = await _clusters.GetForUpdateAsync(id, ct);
        if (cluster is null)
        {
            return null;
        }

        var before = ToDto(cluster);
        Apply(cluster, request, admin.Id);

        await _drafts.MarkDirtyAsync(admin.Id, _timeProvider.GetUtcNow(), ct);
        _auditTrail.Record(AuditActions.Update, AuditEntityType, cluster.ClusterKey, admin.Id, before, ToDto(cluster));
        await SaveAsync(ct);

        _logger.LogInformation("主機群組已修改 {ClusterKey} by {UserId}", cluster.ClusterKey, admin.Id);
        return ToDto(cluster);
    }

    public async Task<bool> DeleteAsync(long? operatorId, long id, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);

        var cluster = await _clusters.GetForUpdateAsync(id, ct);
        if (cluster is null)
        {
            return false;
        }

        if (await _clusters.IsInUseAsync(id, ct))
        {
            throw new InvalidOperationException(ErrorCodes.ClustersInUse);
        }

        _clusters.Remove(cluster);
        await _drafts.MarkDirtyAsync(admin.Id, _timeProvider.GetUtcNow(), ct);
        _auditTrail.Record(AuditActions.Delete, AuditEntityType, cluster.ClusterKey, admin.Id, before: ToDto(cluster));
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("主機群組已刪除 {ClusterKey} by {UserId}", cluster.ClusterKey, admin.Id);
        return true;
    }

    /// <summary>並發新增造成唯一鍵衝突時，轉成與事前檢查相同的 409 錯誤碼</summary>
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new InvalidOperationException(ErrorCodes.ClustersKeyExists, ex);
        }
    }

    private static void ValidatePaging(int pageIndex, int pageSize)
    {
        if (pageIndex < 1 || pageSize < 1 || pageSize > PagingRules.MaxPageSize)
        {
            throw new ArgumentException(ErrorCodes.PagingInvalid);
        }
    }

    private static void ValidateSave(ClusterSaveRequest request)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0 || displayName.Length > ClusterRules.DisplayNameMaxLength)
        {
            throw new ArgumentException(ErrorCodes.ClustersDisplayNameInvalid);
        }

        if (request.LoadBalancingPolicy is not null && !ClusterRules.LoadBalancingPolicyOptions.Contains(request.LoadBalancingPolicy))
        {
            throw new ArgumentException(ErrorCodes.ClustersLoadBalancingPolicyInvalid);
        }

        if (request.HttpVersion is not null && !ClusterRules.HttpVersionOptions.Contains(request.HttpVersion))
        {
            throw new ArgumentException(ErrorCodes.ClustersHttpVersionInvalid);
        }

        if (request.HttpVersionPolicy is not null && !ClusterRules.HttpVersionPolicyOptions.Contains(request.HttpVersionPolicy))
        {
            throw new ArgumentException(ErrorCodes.ClustersHttpVersionPolicyInvalid);
        }

        if (request.ActivityTimeoutSec is < ClusterRules.MinActivityTimeoutSec or > ClusterRules.MaxActivityTimeoutSec)
        {
            throw new ArgumentException(ErrorCodes.ClustersActivityTimeoutInvalid);
        }

        if (request.MaxConnectionsPerServer is < 1)
        {
            throw new ArgumentException(ErrorCodes.ClustersMaxConnectionsInvalid);
        }

        if (request.Description?.Length > ClusterRules.DescriptionMaxLength)
        {
            throw new ArgumentException(ErrorCodes.ClustersDescriptionTooLong);
        }
    }

    private static void Apply(YarpCluster cluster, ClusterSaveRequest request, long operatorId)
    {
        cluster.DisplayName = request.DisplayName!.Trim();
        cluster.LoadBalancingPolicy = request.LoadBalancingPolicy ?? ClusterRules.DefaultLoadBalancingPolicy;
        cluster.HttpVersion = request.HttpVersion ?? ClusterRules.DefaultHttpVersion;
        cluster.HttpVersionPolicy = request.HttpVersionPolicy ?? ClusterRules.DefaultHttpVersionPolicy;
        cluster.ActivityTimeoutSec = request.ActivityTimeoutSec;
        cluster.MaxConnectionsPerServer = request.MaxConnectionsPerServer;
        cluster.AcceptAnyServerCertificate = request.AcceptAnyServerCertificate;
        cluster.IsActive = request.IsActive;
        cluster.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        cluster.UpdatedBy = operatorId;
    }

    private static ClusterDto ToDto(YarpCluster c) => new(
        c.Id,
        c.ClusterKey,
        c.DisplayName,
        c.LoadBalancingPolicy,
        c.HttpVersion,
        c.HttpVersionPolicy,
        c.ActivityTimeoutSec,
        c.MaxConnectionsPerServer,
        c.AcceptAnyServerCertificate,
        c.IsActive,
        c.Description,
        c.UpdatedAt);
}
