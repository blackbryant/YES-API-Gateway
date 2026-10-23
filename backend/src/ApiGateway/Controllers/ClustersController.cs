using ApiGateway.Infrastructure.Http;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>主機群組（範例 CRUD，新模組請比照：只做參數 → Service → HTTP 狀態碼對應）</summary>
[ApiController]
[Route("api/v1/clusters")]
public sealed class ClustersController : ControllerBase
{
    private readonly IClusterService _clusterService;

    public ClustersController(IClusterService clusterService)
    {
        _clusterService = clusterService;
    }

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] ClusterQuery query, CancellationToken ct = default) =>
        HandleAsync(async () => Ok(await _clusterService.SearchAsync(HttpContext.GetOperatorId(), query, ct)));

    [HttpGet("{id:long}")]
    public Task<IActionResult> Get(long id, CancellationToken ct = default) =>
        HandleAsync(async () =>
        {
            var cluster = await _clusterService.GetAsync(HttpContext.GetOperatorId(), id, ct);
            return cluster is null ? NotFoundResult() : Ok(ApiResponse.Ok(cluster));
        });

    [HttpPost]
    public Task<IActionResult> Create([FromBody] ClusterSaveRequest request, CancellationToken ct = default) =>
        HandleAsync(async () =>
            StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(await _clusterService.CreateAsync(HttpContext.GetOperatorId(), request, ct))));

    [HttpPut("{id:long}")]
    public Task<IActionResult> Update(long id, [FromBody] ClusterSaveRequest request, CancellationToken ct = default) =>
        HandleAsync(async () =>
        {
            var cluster = await _clusterService.UpdateAsync(HttpContext.GetOperatorId(), id, request, ct);
            return cluster is null ? NotFoundResult() : Ok(ApiResponse.Ok(cluster));
        });

    [HttpDelete("{id:long}")]
    public Task<IActionResult> Delete(long id, CancellationToken ct = default) =>
        HandleAsync(async () =>
            await _clusterService.DeleteAsync(HttpContext.GetOperatorId(), id, ct) ? NoContent() : NotFoundResult());

    private ObjectResult NotFoundResult() =>
        NotFound(ApiResponse.Fail(ErrorCodes.ClustersNotFound, "Cluster not found"));

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
        catch (InvalidOperationException ex) when (ex.Message is ErrorCodes.ClustersKeyExists or ErrorCodes.ClustersInUse)
        {
            return Conflict(ApiResponse.Fail(ex.Message, "Conflict"));
        }
    }
}
