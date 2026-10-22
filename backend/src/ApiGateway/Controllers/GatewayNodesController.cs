using ApiGateway.Infrastructure.Http;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

/// <summary>閘道節點同步狀態（唯讀）</summary>
[ApiController]
[Route("api/v1/gateway-nodes")]
public sealed class GatewayNodesController : ControllerBase
{
    private readonly IGatewayNodeService _nodeService;

    public GatewayNodesController(IGatewayNodeService nodeService)
    {
        _nodeService = nodeService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        try
        {
            return Ok(ApiResponse.Ok(await _nodeService.ListAsync(HttpContext.GetOperatorId(), ct)));
        }
        catch (UnauthorizedAccessException ex) when (ex.Message == ErrorCodes.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message, "Forbidden"));
        }
    }
}
