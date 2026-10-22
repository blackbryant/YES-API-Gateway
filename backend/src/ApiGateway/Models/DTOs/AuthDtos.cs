namespace ApiGateway.Models.DTOs;

public sealed record LoginRequest(string? Username, string? Password);

/// <summary>目前工作階段（對應前端 UserSession）</summary>
public sealed record UserSessionDto(
    string Username,
    string DisplayName,
    string Role,
    DateTimeOffset LoggedInAt,
    bool MustChangePassword);

/// <summary>登入結果；AccessToken 由 Controller 寫入 HttpOnly Cookie，不回傳給前端</summary>
public sealed record LoginResult(UserSessionDto Session, string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>AuthMiddleware 驗證 token 後寫入 HttpContext.Items["AuthUser"] 的內容</summary>
public sealed record AuthTokenPayload(long UserId, string Username, string Role, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
