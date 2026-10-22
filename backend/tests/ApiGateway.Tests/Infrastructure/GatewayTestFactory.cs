using System.Security.Cryptography;
using ApiGateway.Gateway;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Yarp.ReverseProxy;

namespace ApiGateway.Tests.Infrastructure;

/// <summary>
/// 閘道整合測試：不連資料庫（連線字串指向不存在的埠），移除所有背景工作，
/// 直接以 ApplyAsync 套用 YARP 設定；時間由 FakeTimeProvider 控制。
/// </summary>
public class GatewayTestFactory : WebApplicationFactory<Program>
{
    private static readonly TimeSpan ApplyTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    private int _versionSeq;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 22, 1, 0, 0, TimeSpan.Zero));

    /// <summary>額外設定（例：ReverseProxy:Routes 靜態路由）</summary>
    public Dictionary<string, string?> Settings { get; } = new();

    /// <summary>額外替換服務（例：以 Mock 取代需要資料庫的 Repository）</summary>
    public Action<IServiceCollection>? ConfigureTestServices { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:GatewayDb", "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=1");
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            ConfigureTestServices?.Invoke(services);
        });
    }

    public DatabaseProxyConfigProvider Provider => Services.GetRequiredService<DatabaseProxyConfigProvider>();

    /// <summary>套用設定並等待 YARP 完成重新載入</summary>
    public async Task ApplyAsync(YarpConfigDocument document)
    {
        var version = $"T{Interlocked.Increment(ref _versionSeq):D9}";
        Provider.ApplyDocument(document, version, version);
        await WaitUntilAppliedAsync(document);
    }

    private async Task WaitUntilAppliedAsync(YarpConfigDocument document)
    {
        var lookup = Services.GetRequiredService<IProxyStateLookup>();
        var deadline = DateTime.UtcNow + ApplyTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var routesReady = document.Routes.All(r => lookup.TryGetRoute(r.RouteId, out var model) && model.Config == r);
            var clustersReady = document.Clusters.All(c => lookup.TryGetCluster(c.ClusterId, out var state) && state.Model.Config == c);
            var noStaleRoutes = lookup.GetRoutes().All(r => document.Routes.Any(x => x.RouteId == r.Config.RouteId) || r.Config.RouteId.StartsWith("static-", StringComparison.Ordinal));
            if (routesReady && clustersReady && noStaleRoutes)
            {
                return;
            }

            await Task.Delay(PollInterval);
        }

        throw new TimeoutException("YARP did not apply the test configuration in time.");
    }
}
