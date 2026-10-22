using ApiGateway.Infrastructure.Auth;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;

namespace ApiGateway.Middleware;

/// <summary>
/// Admin API 認證：/api/v1 下除 AuthRules.AnonymousPaths 外一律需要登入（預設拒絕）。
/// token 取自 HttpOnly Cookie，或 Authorization: Bearer（供 .http 測試與工具使用）。
/// 成功時寫入 HttpContext.Items["AuthUser"]；角色權限由 Service 判斷。
/// 閘道代理流量不經此驗證，由路由的授權策略處理。
/// </summary>
public sealed class AuthMiddleware
{
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly ILogger<AuthMiddleware> _logger;

    public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
    {
        if (!RequiresAuthentication(context.Request))
        {
            await _next(context);
            return;
        }

        var token = ReadToken(context.Request);
        if (token is null)
        {
            await WriteUnauthorizedAsync(context, ErrorCodes.AuthUnauthenticated, "Not signed in");
            return;
        }

        var payload = await tokenService.ValidateAsync(token);
        if (payload is null)
        {
            _logger.LogWarning("Security: 無效的 access token {Path} ip={ClientIp}", context.Request.Path.Value, context.Connection.RemoteIpAddress);
            await WriteUnauthorizedAsync(context, ErrorCodes.AuthAccessTokenInvalid, "Access token is invalid or expired");
            return;
        }

        context.Items[AuthRules.AuthUserItemKey] = payload;
        await _next(context);
    }

    public static bool RequiresAuthentication(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments(AuthRules.AdminApiPrefix) || HttpMethods.IsOptions(request.Method))
        {
            return false;
        }

        var path = request.Path.Value?.TrimEnd('/') ?? string.Empty;
        return !AuthRules.AnonymousPaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ReadToken(HttpRequest request)
    {
        if (request.Cookies.TryGetValue(AuthRules.AccessTokenCookieName, out var cookie) && !string.IsNullOrWhiteSpace(cookie))
        {
            return cookie;
        }

        var header = request.Headers.Authorization.ToString();
        return header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) ? header[BearerPrefix.Length..].Trim() : null;
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string code, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(code, message));
    }
}
