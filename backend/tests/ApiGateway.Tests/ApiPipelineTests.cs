using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ApiGateway.Models.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiGateway.Tests;

/// <summary>
/// Middleware 管線冒煙測試：不連資料庫（連線字串指向不存在的埠，背景初始化只會記警告）。
/// </summary>
public class ApiPipelineTests : IClassFixture<ApiPipelineTests.Factory>
{
    private readonly HttpClient _client;

    public ApiPipelineTests(Factory factory)
    {
        _client = factory.CreateClient();
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:GatewayDb", "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=1");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        }
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        return doc.RootElement.GetProperty("code").GetString();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/clusters");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthUnauthenticated, await ReadCodeAsync(response));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/clusters");
        request.Headers.Authorization = new("Bearer", "invalid");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthAccessTokenInvalid, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Login_MissingCredentials_Returns400WithoutTouchingDatabase()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { username = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthCredentialsRequired, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task UnknownApiPath_Returns404Json()
    {
        var response = await _client.GetAsync("/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task HealthCheck_Returns200AndHidesServerHeader()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Server"));
    }
}
