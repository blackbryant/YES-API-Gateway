using System.Diagnostics;
using ApiGateway.Infrastructure.Http;

namespace ApiGateway.Middleware;

/// <summary>
/// /api 回應為 4xx / 5xx 時記錄請求摘要。
/// 不記錄 request / response body，避免密碼、token 等機密寫入日誌（憲章八）。
/// </summary>
public sealed class RequestResponseLoggingMiddleware
{
    private const int ClientErrorStatusCode = 400;

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        await _next(context);

        if (context.Response.StatusCode >= ClientErrorStatusCode)
        {
            _logger.LogWarning(
                "API {Method} {Path}{QueryString} → {StatusCode} ({ElapsedMs:0} ms) user={UserId} ip={ClientIp} trace={TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                context.GetOperatorId(),
                context.Connection.RemoteIpAddress,
                context.TraceIdentifier);
        }
    }
}
