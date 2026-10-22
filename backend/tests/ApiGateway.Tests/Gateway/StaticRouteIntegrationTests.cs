using System.Net;
using System.Net.Http.Json;
using ApiGateway.Gateway;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Tests.Infrastructure;

namespace ApiGateway.Tests.Gateway;

/// <summary>gateway-behavior.md G-01 ～ G-05：健康檢查與系統端點不受動態路由與資料庫影響</summary>
[Collection(WebHostCollection.Name)]
public sealed class StaticRouteIntegrationTests : IAsyncLifetime
{
    private const string Host = "api.example.com";

    private readonly GatewayTestFactory _factory = new();
    private FakeBackend _dynamic = null!;
    private FakeBackend _static = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _dynamic = await FakeBackend.StartAsync("dynamic");
        _static = await FakeBackend.StartAsync("static");

        // appsettings 的 ReverseProxy 區段（靜態代理路由）
        _factory.Settings["ReverseProxy:Routes:static-echo:ClusterId"] = "static-cluster";
        _factory.Settings["ReverseProxy:Routes:static-echo:Match:Path"] = "/static/{**rest}";
        _factory.Settings["ReverseProxy:Clusters:static-cluster:Destinations:d1:Address"] = _static.Address;
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _dynamic.DisposeAsync();
        await _static.DisposeAsync();
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? host = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task G01_G02_HealthCheck_WithoutVersionOrDatabase_Returns200()
    {
        using var response = await GetAsync(HealthCheckOptions.DefaultPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Null(_factory.Provider.LoadedVersionNo);
    }

    [Fact]
    public async Task G03_CatchAllDynamicRoute_DoesNotShadowSystemEndpoints()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster(_dynamic.Address);
        f.AddRoute(f.AddService("site", clusterId: cluster.Id), "/{**catch-all}", hosts: [Host]);
        await _factory.ApplyAsync(YarpConfigBuilder.Build(f.Build()));

        using var health = await GetAsync(HealthCheckOptions.DefaultPath, Host);
        Assert.Equal("Healthy", await health.Content.ReadAsStringAsync());

        using var adminApi = await GetAsync("/api/v1/auth/me", Host);
        Assert.Equal(HttpStatusCode.Unauthorized, adminApi.StatusCode);

        using var other = await GetAsync("/anything", Host);
        Assert.Equal("dynamic", (await other.Content.ReadFromJsonAsync<FakeBackendResponse>())!.Name);
        Assert.Equal(1, _dynamic.RequestCount);
    }

    [Fact]
    public async Task G05_StaticProxyRoute_FromAppSettings()
    {
        using var response = await GetAsync("/static/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("static", (await response.Content.ReadFromJsonAsync<FakeBackendResponse>())!.Name);
    }

    [Fact]
    public async Task G05_StaticRoute_SurvivesDynamicConfigChange()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster(_dynamic.Address);
        f.AddRoute(f.AddService("svc", clusterId: cluster.Id), "/api/orders/{**rest}");
        await _factory.ApplyAsync(YarpConfigBuilder.Build(f.Build()));

        using var response = await GetAsync("/static/ping");

        Assert.Equal("static", (await response.Content.ReadFromJsonAsync<FakeBackendResponse>())!.Name);
    }
}
