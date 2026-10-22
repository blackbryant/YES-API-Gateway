using ApiGateway.Infrastructure.Http;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>服務（入口、主機、A/B 分流）；異動只寫入草稿，發佈後生效</summary>
[ApiController]
[Route("api/v1/services")]
public sealed class ServicesController : ControllerBase
{
    private readonly IServiceService _serviceService;

    public ServicesController(IServiceService serviceService)
    {
        _serviceService = serviceService;
    }

    [HttpGet]
    public Task<IActionResult> Search([FromQuery] ServiceQuery query, CancellationToken ct = default) =>
        HandleAsync(async () => Ok(await _serviceService.SearchAsync(HttpContext.GetOperatorId(), query, ct)));

    [HttpGet("{id:long}")]
    public Task<IActionResult> Get(long id, CancellationToken ct = default) =>
        HandleAsync(async () =>
        {
            var service = await _serviceService.GetAsync(HttpContext.GetOperatorId(), id, ct);
            return service is null ? NotFoundResult() : Ok(ApiResponse.Ok(service));
        });

    [HttpPost]
    public Task<IActionResult> Create([FromBody] ServiceSaveRequest request, CancellationToken ct = default) =>
        HandleAsync(async () =>
            StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(await _serviceService.CreateAsync(HttpContext.GetOperatorId(), request, ct))));

    [HttpPut("{id:long}")]
    public Task<IActionResult> Update(long id, [FromBody] ServiceSaveRequest request, CancellationToken ct = default) =>
        HandleAsync(async () =>
        {
            var service = await _serviceService.UpdateAsync(HttpContext.GetOperatorId(), id, request, ct);
            return service is null ? NotFoundResult() : Ok(ApiResponse.Ok(service));
        });

    [HttpPut("{id:long}/active")]
    public Task<IActionResult> SetActive(long id, [FromBody] ServiceActiveRequest request, CancellationToken ct = default) =>
        HandleAsync(async () =>
            await _serviceService.SetActiveAsync(HttpContext.GetOperatorId(), id, request.IsActive, ct) ? NoContent() : NotFoundResult());

    [HttpDelete("{id:long}")]
    public Task<IActionResult> Delete(long id, CancellationToken ct = default) =>
        HandleAsync(async () =>
            await _serviceService.DeleteAsync(HttpContext.GetOperatorId(), id, ct) ? NoContent() : NotFoundResult());

    private ObjectResult NotFoundResult() =>
        NotFound(ApiResponse.Fail(ErrorCodes.ServicesNotFound, "Service not found"));

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
        catch (InvalidOperationException ex) when (ex.Message is ErrorCodes.ServicesKeyExists or ErrorCodes.ClustersInUse)
        {
            return Conflict(ApiResponse.Fail(ex.Message, "Conflict"));
        }
    }
}
