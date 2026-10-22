using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;

namespace ApiGateway.Services.Implementations;

/// <summary>每次以資料庫最新的帳號狀態判斷權限，停用或降權立即生效</summary>
public sealed class AccessControlService : IAccessControlService
{
    private readonly IAppUserRepository _users;
    private readonly ILogger<AccessControlService> _logger;

    public AccessControlService(IAppUserRepository users, ILogger<AccessControlService> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<AppUser> EnsureSignedInAsync(long? userId, CancellationToken ct = default)
    {
        var user = userId is null ? null : await _users.GetByIdAsync(userId.Value, ct);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Security: 拒絕存取，帳號不存在或已停用 {UserId}", userId);
            throw new UnauthorizedAccessException(ErrorCodes.Forbidden);
        }

        return user;
    }

    public async Task<AppUser> EnsureAdminAsync(long? userId, CancellationToken ct = default)
    {
        var user = await EnsureSignedInAsync(userId, ct);
        if (user.Role != RoleCodes.Admin)
        {
            _logger.LogWarning("Security: 拒絕存取，非管理者 {UserId}", userId);
            throw new UnauthorizedAccessException(ErrorCodes.Forbidden);
        }

        return user;
    }
}
