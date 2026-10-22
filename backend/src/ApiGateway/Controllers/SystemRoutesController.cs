using ApiGateway.Infrastructure.Http;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>靜態路由與系統保留路徑（唯讀）</summary>
[ApiController]
[Route("api/v1/system-routes")]
public sealed class SystemRoutesController : ControllerBase
{
    private readonly ISystemRouteService _systemRouteService;

    public SystemRoutesController(ISystemRouteService systemRouteService)
    {
        _systemRouteService = systemRouteService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        try
        {
            return Ok(ApiResponse.Ok(await _systemRouteService.GetAsync(HttpContext.GetOperatorId(), ct)));
        }
        catch (UnauthorizedAccessException ex) when (ex.Message == ErrorCodes.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message, "Forbidden"));
        }
    }
}
