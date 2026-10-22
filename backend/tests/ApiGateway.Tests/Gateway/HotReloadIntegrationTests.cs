using System.Net.Http.Json;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Tests.Infrastructure;

namespace ApiGateway.Tests.Gateway;

/// <summary>gateway-behavior.md G-20、G-21：熱更新不中斷、進行中的請求依原設定完成</summary>
[Collection(WebHostCollection.Name)]
public sealed class HotReloadIntegrationTests : IAsyncLifetime
{
    private const string Host = "api.example.com";
    private const int ConcurrentClients = 5;

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

    private Task ApplyAsync(FakeBackend backend)
    {
        var f = new SnapshotFixture();
        var cluster = f.AddCluster(backend.Address);
        f.AddRoute(f.AddService("orders-api", clusterId: cluster.Id), "/api/orders/{**rest}", hosts: [Host]);
        return _factory.ApplyAsync(YarpConfigBuilder.Build(f.Build()));
    }

    private async Task<(bool Ok, string? Name)> GetAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders/1");
        request.Headers.Host = Host;
        using var response = await _client.SendAsync(request);
        return response.IsSuccessStatusCode
            ? (true, (await response.Content.ReadFromJsonAsync<FakeBackendResponse>())!.Name)
            : (false, null);
    }

    [Fact]
    public async Task G20_SwitchClusterUnderLoad_NoFailedRequests()
    {
        await ApplyAsync(_a);
        using var stop = new CancellationTokenSource();
        var failures = 0;
        var load = Enumerable.Range(0, ConcurrentClients).Select(_ => Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                if (!(await GetAsync()).Ok)
                {
                    Interlocked.Increment(ref failures);
                }
            }
        })).ToList();

        await Task.Delay(300);
        await ApplyAsync(_b);
        await Task.Delay(300);
        await stop.CancelAsync();
        await Task.WhenAll(load);

        Assert.Equal(0, failures);
        Assert.Equal("B", (await GetAsync()).Name);
        Assert.True(_a.RequestCount > 0 && _b.RequestCount > 0);
    }

    [Fact]
    public async Task G21_InFlightRequest_CompletesOnOldCluster()
    {
        _a.Delay = TimeSpan.FromSeconds(2);
        await ApplyAsync(_a);

        var inFlight = GetAsync();
        await Task.Delay(300);
        await ApplyAsync(_b);

        var result = await inFlight;
        Assert.True(result.Ok);
        Assert.Equal("A", result.Name);
        Assert.Equal("B", (await GetAsync()).Name);
    }
}
