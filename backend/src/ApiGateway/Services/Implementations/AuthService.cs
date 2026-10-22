using ApiGateway.Infrastructure.Auth;
using ApiGateway.Infrastructure.Exceptions;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ApiGateway.Services.Implementations;

/// <summary>管理介面登入（F-15）：PasswordHasher 驗證、連續失敗 5 次鎖定、登入稽核</summary>
public sealed class AuthService : IAuthService
{
    private const string AuditEntityType = nameof(AppUser) + "s";

    private readonly IAppUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditTrailService _auditTrail;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAppUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher<AppUser> passwordHasher,
        ITokenService tokenService,
        IAuditTrailService auditTrail,
        TimeProvider timeProvider,
        ILogger<AuthService> logger)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditTrail = auditTrail;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var (username, password) = ValidateLogin(request);
        var now = _timeProvider.GetUtcNow();
        var user = await _users.GetByUsernameForUpdateAsync(username, ct);

        if (user is null || !user.IsActive)
        {
            await RecordFailureAsync(null, username, "unknown or inactive user", ct);
            throw new AuthenticationFailedException(ErrorCodes.AuthInvalidCredentials);
        }

        if (user.LockoutEndAt > now)
        {
            await RecordFailureAsync(user.Id, username, "account locked", ct);
            throw new AuthenticationFailedException(ErrorCodes.AuthAccountLocked);
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount += 1;
            var locked = user.FailedLoginCount >= AuthRules.MaxFailedLoginCount;
            if (locked)
            {
                user.FailedLoginCount = 0;
                user.LockoutEndAt = now.AddMinutes(AuthRules.LockoutMinutes);
            }

            await RecordFailureAsync(user.Id, username, locked ? "wrong password, account locked" : "wrong password", ct);
            throw new AuthenticationFailedException(locked ? ErrorCodes.AuthAccountLocked : ErrorCodes.AuthInvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = now;
        _auditTrail.Record(AuditActions.Login, AuditEntityType, user.Username, user.Id);
        await _unitOfWork.SaveChangesAsync(ct);

        var (token, expiresAt) = _tokenService.CreateAccessToken(user);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Security: 登入成功 {UserId} {Username}", user.Id, user.Username);
        }

        return new LoginResult(ToSession(user, now), token, expiresAt);
    }

    public async Task<UserSessionDto?> GetCurrentUserAsync(AuthTokenPayload payload, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(payload.UserId, ct);
        return user is { IsActive: true } ? ToSession(user, payload.IssuedAt) : null;
    }

    private static (string Username, string Password) ValidateLogin(LoginRequest request)
    {
        var username = request.Username?.Trim();
        var password = request.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)
            || username.Length > AuthRules.UsernameMaxLength || password.Length > AuthRules.PasswordMaxLength)
        {
            throw new ArgumentException(ErrorCodes.AuthCredentialsRequired);
        }

        return (username, password);
    }

    private async Task RecordFailureAsync(long? userId, string username, string reason, CancellationToken ct)
    {
        _auditTrail.Record(AuditActions.LoginFailed, AuditEntityType, username, userId);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogWarning("Security: 登入失敗 {Username} {Reason}", username, reason);
    }

    private static UserSessionDto ToSession(AppUser user, DateTimeOffset loggedInAt) =>
        new(user.Username, user.DisplayName, user.Role, loggedInAt, user.MustChangePassword);
}
