using System.Net;
using System.Net.Http.Json;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Tests.Infrastructure;

namespace ApiGateway.Tests.Gateway;

/// <summary>gateway-behavior.md G-10 ～ G-16：動態路由比對（不需資料庫）</summary>
[Collection(WebHostCollection.Name)]
public sealed class GatewayRoutingIntegrationTests : IAsyncLifetime
{
    private readonly GatewayTestFactory _factory = new();
    private FakeBackend _a = null!;
    private FakeBackend _b = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _a = await FakeBackend.StartAsync("A");
        _b = await FakeBackend.StartAsync("B");
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _a.DisposeAsync();
        await _b.DisposeAsync();
    }

    private Task ApplyAsync(SnapshotFixture f) => _factory.ApplyAsync(YarpConfigBuilder.Build(f.Build()));

    private async Task<(HttpStatusCode Status, FakeBackendResponse? Body)> SendAsync(HttpMethod method, string url, string? host = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        using var response = await _client.SendAsync(request);
        var isJson = response.Content.Headers.ContentType?.MediaType == "application/json";
        var body = response.IsSuccessStatusCode && isJson ? await response.Content.ReadFromJsonAsync<FakeBackendResponse>() : null;
        return (response.StatusCode, body);
    }

    [Fact]
    public async Task G10_G11_G12_HostAndMethodMatching()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster(_a.Address);
        f.AddRoute(f.AddService("orders-api", clusterId: cluster.Id), "/api/orders/{**catch-all}", hosts: ["api.example.com"], methods: ["GET", "POST"]);
        await ApplyAsync(f);

        var ok = await SendAsync(HttpMethod.Get, "/api/orders/1", "api.example.com");
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal("A", ok.Body!.Name);

        var before = _a.RequestCount;
        var wrongMethod = await SendAsync(HttpMethod.Delete, "/api/orders/1", "api.example.com");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.Status);

        // 未比對到動態路由：不轉送（管理介面 SPA fallback 可能回 index.html）
        var wrongHost = await SendAsync(HttpMethod.Get, "/api/orders/1", "other.example.com");
        Assert.Null(wrongHost.Body);
        Assert.Equal(before, _a.RequestCount);
    }

    [Fact]
    public async Task G13_EmptyMethods_AcceptsPatch()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster(_a.Address);
        f.AddRoute(f.AddService("orders-api", clusterId: cluster.Id), "/api/orders/{**catch-all}", hosts: ["api.example.com"]);
        await ApplyAsync(f);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Patch, "/api/orders/1", "api.example.com")).Status);
    }

    [Fact]
    public async Task G14_LowerOrderWins()
    {
        var f = new SnapshotFixture();
        var clusterA = f.AddCluster(_a.Address);
        var clusterB = f.AddCluster(_b.Address);
        var service = f.AddService("svc", clusterId: clusterA.Id);
        f.AddRoute(service, "/api/{**rest}", hosts: ["api.example.com"], order: 200);
        f.AddRoute(service, "/api/orders/{**rest}", hosts: ["api.example.com"], order: 100, clusterId: clusterB.Id);
        await ApplyAsync(f);

        Assert.Equal("B", (await SendAsync(HttpMethod.Get, "/api/orders/1", "api.example.com")).Body!.Name);
        Assert.Equal("A", (await SendAsync(HttpMethod.Get, "/api/users/1", "api.example.com")).Body!.Name);
    }

    [Fact]
    public async Task G15_PathRemovePrefix()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster(_a.Address);
        var route = f.AddRoute(f.AddService("svc", clusterId: cluster.Id), "/api/orders/{**rest}", hosts: ["api.example.com"]);
        f.AddPrefix(route, "/api/orders");
        await ApplyAsync(f);

        var result = await SendAsync(HttpMethod.Get, "/api/orders/1?x=1", "api.example.com");

        Assert.Equal("/1", result.Body!.Path);
        Assert.Equal("?x=1", result.Body.Query);
    }

    [Fact]
    public async Task G16_InactiveRoute_NotForwarded()
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster(_a.Address);
        var service = f.AddService("svc", clusterId: cluster.Id);
        f.AddRoute(service, "/api/on/{**rest}", hosts: ["api.example.com"]);
        f.AddRoute(service, "/api/off/{**rest}", hosts: ["api.example.com"], active: false);
        await ApplyAsync(f);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, "/api/on/1", "api.example.com")).Status);
        var before = _a.RequestCount;
        Assert.Null((await SendAsync(HttpMethod.Get, "/api/off/1", "api.example.com")).Body);
        Assert.Equal(before, _a.RequestCount);
    }
}
