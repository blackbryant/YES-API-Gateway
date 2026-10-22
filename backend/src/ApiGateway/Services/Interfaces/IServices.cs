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

public interface IClusterService
{
    Task<PagedResponse<ClusterDto>> SearchAsync(long? operatorId, ClusterQuery query, CancellationToken ct = default);

    Task<ClusterDto?> GetAsync(long? operatorId, long id, CancellationToken ct = default);

    Task<ClusterDto> CreateAsync(long? operatorId, ClusterSaveRequest request, CancellationToken ct = default);

    Task<ClusterDto?> UpdateAsync(long? operatorId, long id, ClusterSaveRequest request, CancellationToken ct = default);

    Task<bool> DeleteAsync(long? operatorId, long id, CancellationToken ct = default);
}
