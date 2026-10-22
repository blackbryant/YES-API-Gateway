using ApiGateway.Infrastructure.Exceptions;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;

namespace ApiGateway.Middleware;

/// <summary>RequestValidationException → 400；其他未處理例外 → 500（不回傳例外細節）</summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (RequestValidationException ex) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(ex.Message, "Validation failed", ex.Errors));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // 用戶端中斷連線，不視為錯誤
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(ex, "未處理的例外 {Method} {Path} trace={TraceId}", context.Request.Method, context.Request.Path.Value, context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(ErrorCodes.InternalError, "Internal server error"));
        }
    }
}
