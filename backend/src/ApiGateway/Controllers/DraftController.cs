using ApiGateway.Infrastructure.Http;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/v1/draft")]
public sealed class DraftController : ControllerBase
{
    private readonly IDraftService _draftService;

    public DraftController(IDraftService draftService)
    {
        _draftService = draftService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct = default)
    {
        try
        {
            return Ok(ApiResponse.Ok(await _draftService.GetStatusAsync(HttpContext.GetOperatorId(), ct)));
        }
        catch (UnauthorizedAccessException ex) when (ex.Message == ErrorCodes.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message, "Forbidden"));
        }
    }
}
