using ApiGateway.Infrastructure.Exceptions;
using ApiGateway.Infrastructure.Http;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>發佈（生效）：預覽變更與驗證結果、產生新版本並熱更新</summary>
[ApiController]
[Route("api/v1/publish")]
public sealed class PublishController : ControllerBase
{
    private readonly IPublishService _publishService;

    public PublishController(IPublishService publishService)
    {
        _publishService = publishService;
    }

    [HttpGet("preview")]
    public Task<IActionResult> Preview(CancellationToken ct = default) =>
        HandleAsync(async () => Ok(ApiResponse.Ok(await _publishService.GetPreviewAsync(HttpContext.GetOperatorId(), ct))));

    [HttpPost]
    public Task<IActionResult> Publish([FromBody] PublishRequest request, CancellationToken ct = default) =>
        HandleAsync(async () =>
            StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(await _publishService.PublishAsync(HttpContext.GetOperatorId(), request, ct))));

    /// <summary>Service 例外 → HTTP 狀態碼（見 Architecture.md 3.3）</summary>
    private async Task<IActionResult> HandleAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (PublishValidationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message, "Publish validation failed", new { errors = ex.Issues }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message, "Validation failed"));
        }
        catch (UnauthorizedAccessException ex) when (ex.Message == ErrorCodes.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message, "Forbidden"));
        }
        catch (InvalidOperationException ex) when (ex.Message is ErrorCodes.PublishInProgress or ErrorCodes.PublishDraftChanged or ErrorCodes.PublishDailyLimit)
        {
            return Conflict(ApiResponse.Fail(ex.Message, "Conflict"));
        }
        catch (InvalidOperationException ex) when (ex.Message == ErrorCodes.PublishStorageUnavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Fail(ex.Message, "Storage unavailable"));
        }
    }
}
