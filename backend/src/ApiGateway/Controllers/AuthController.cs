using ApiGateway.Infrastructure.Exceptions;
using ApiGateway.Infrastructure.Http;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Responses;
using ApiGateway.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const string CookiePath = "/api";

    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct = default)
    {
        try
        {
            var result = await _authService.LoginAsync(request, ct);
            Response.Cookies.Append(AuthRules.AccessTokenCookieName, result.AccessToken, BuildCookieOptions(result.ExpiresAt));
            return Ok(ApiResponse.Ok(result.Session));
        }
        catch (ArgumentException ex) when (ex.Message == ErrorCodes.AuthCredentialsRequired)
        {
            return BadRequest(ApiResponse.Fail(ex.Message, "Username and password are required"));
        }
        catch (AuthenticationFailedException ex) when (ex.Message == ErrorCodes.AuthAccountLocked)
        {
            return StatusCode(StatusCodes.Status423Locked, ApiResponse.Fail(ex.Message, "Account is locked"));
        }
        catch (AuthenticationFailedException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message, "Invalid username or password"));
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthRules.AccessTokenCookieName, BuildCookieOptions(null));
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct = default)
    {
        var payload = HttpContext.GetAuthUser();
        var session = payload is null ? null : await _authService.GetCurrentUserAsync(payload, ct);
        return session is null
            ? Unauthorized(ApiResponse.Fail(ErrorCodes.AuthUnauthenticated, "Not signed in"))
            : Ok(ApiResponse.Ok(session));
    }

    private CookieOptions BuildCookieOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expiresAt,
        IsEssential = true,
    };
}
