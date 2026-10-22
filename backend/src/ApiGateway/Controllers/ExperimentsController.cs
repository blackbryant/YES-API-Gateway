using ApiGateway.Infrastructure.Http;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>A/B 實驗：總覽摘要、需要處理項目、手動暫停 / 恢復 / 結束</summary>
[ApiController]
[Route("api/v1/experiments")]
public sealed class ExperimentsController : ControllerBase
{
    private readonly IExperimentService _experimentService;

    public ExperimentsController(IExperimentService experimentService)
    {
        _experimentService = experimentService;
    }

    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken ct = default) =>
        HandleAsync(async () => Ok(ApiResponse.Ok(await _experimentService.GetSummaryAsync(HttpContext.GetOperatorId(), ct))));

    [HttpGet("attention")]
    public Task<IActionResult> Attention(CancellationToken ct = default) =>
        HandleAsync(async () => Ok(ApiResponse.Ok(await _experimentService.GetAttentionItemsAsync(HttpContext.GetOperatorId(), ct))));

    [HttpPost("{experimentKey}/runtime-actions")]
    public Task<IActionResult> RunAction(string experimentKey, [FromBody] ExperimentRuntimeActionRequest request, CancellationToken ct = default) =>
        HandleAsync(async () =>
        {
            var result = await _experimentService.RunActionAsync(HttpContext.GetOperatorId(), experimentKey, request.Action, ct);
            return result is null
                ? NotFound(ApiResponse.Fail(ErrorCodes.ExperimentsNotFound, "Experiment is not published"))
                : Ok(ApiResponse.Ok(result));
        });

    /// <summary>Service 例外 → HTTP 狀態碼（見 Architecture.md 3.3）</summary>
    private async Task<IActionResult> HandleAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message, "Validation failed"));
        }
        catch (UnauthorizedAccessException ex) when (ex.Message == ErrorCodes.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message, "Forbidden"));
        }
    }
}
