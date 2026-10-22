using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Models.Responses;

namespace ApiGateway.Services.Interfaces;

/// <summary>Service 第一行呼叫的權限檢查；不符時丟 UnauthorizedAccessException(ErrorCodes.Forbidden)</summary>
public interface IAccessControlService
{
    /// <summary>已登入且帳號啟用（admin / user 皆可，唯讀操作使用）</summary>
    Task<AppUser> EnsureSignedInAsync(long? userId, CancellationToken ct = default);

    /// <summary>admin 角色（編輯、發佈、回滾使用）</summary>
    Task<AppUser> EnsureAdminAsync(long? userId, CancellationToken ct = default);
}

/// <summary>稽核日誌暫存（隨 Service 的 SaveChanges 同一交易寫入）</summary>
public interface IAuditTrailService
{
    void Record(string action, string entityType, string? entityKey, long? userId, object? before = null, object? after = null);
}

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<UserSessionDto?> GetCurrentUserAsync(AuthTokenPayload payload, CancellationToken ct = default);
}

public interface IDraftService
{
    Task<DraftStatusDto> GetStatusAsync(long? operatorId, CancellationToken ct = default);
}

/// <summary>服務（入口、主機、A/B 分流）維護</summary>
public interface IServiceService
{
    Task<PagedResponse<ServiceSummaryDto>> SearchAsync(long? operatorId, ServiceQuery query, CancellationToken ct = default);

    Task<ServiceDetailDto?> GetAsync(long? operatorId, long id, CancellationToken ct = default);

    Task<ServiceDetailDto> CreateAsync(long? operatorId, ServiceSaveRequest request, CancellationToken ct = default);

    Task<ServiceDetailDto?> UpdateAsync(long? operatorId, long id, ServiceSaveRequest request, CancellationToken ct = default);

    Task<bool> SetActiveAsync(long? operatorId, long id, bool isActive, CancellationToken ct = default);

    Task<bool> DeleteAsync(long? operatorId, long id, CancellationToken ct = default);
}

/// <summary>服務頁的讀取協作元件</summary>
public interface IServiceReadSupport
{
    DateTimeOffset Now { get; }

    Task<DateTimeOffset?> GetActivePublishedAtAsync(CancellationToken ct = default);

    Gateway.Experiments.ExperimentState GetExperimentState(string experimentKey);
}

/// <summary>A/B 實驗的執行期查詢與手動操作</summary>
public interface IExperimentService
{
    Task<IReadOnlyList<ExperimentSummaryDto>> GetSummaryAsync(long? operatorId, CancellationToken ct = default);

    Task<IReadOnlyList<AttentionItemDto>> GetAttentionItemsAsync(long? operatorId, CancellationToken ct = default);

    /// <summary>實驗不在生效版本中時回傳 null</summary>
    Task<ExperimentSummaryDto?> RunActionAsync(long? operatorId, string experimentKey, Models.Constants.ExperimentRuntimeAction action, CancellationToken ct = default);
}

/// <summary>靜態路由與系統保留路徑（唯讀）</summary>
public interface ISystemRouteService
{
    Task<SystemRoutesDto> GetAsync(long? operatorId, CancellationToken ct = default);
}

/// <summary>閘道節點同步狀態</summary>
public interface IGatewayNodeService
{
    Task<IReadOnlyList<GatewayNodeDto>> ListAsync(long? operatorId, CancellationToken ct = default);
}

/// <summary>發佈（草稿 → 驗證 → 版本 → 熱更新）</summary>
public interface IPublishService
{
    Task<PublishPreviewDto> GetPreviewAsync(long? operatorId, CancellationToken ct = default);

    Task<PublishResultDto> PublishAsync(long? operatorId, PublishRequest request, CancellationToken ct = default);
}

public interface IClusterService
{
    Task<PagedResponse<ClusterDto>> SearchAsync(long? operatorId, ClusterQuery query, CancellationToken ct = default);

    Task<ClusterDto?> GetAsync(long? operatorId, long id, CancellationToken ct = default);

    Task<ClusterDto> CreateAsync(long? operatorId, ClusterSaveRequest request, CancellationToken ct = default);

    Task<ClusterDto?> UpdateAsync(long? operatorId, long id, ClusterSaveRequest request, CancellationToken ct = default);

    Task<bool> DeleteAsync(long? operatorId, long id, CancellationToken ct = default);
}
