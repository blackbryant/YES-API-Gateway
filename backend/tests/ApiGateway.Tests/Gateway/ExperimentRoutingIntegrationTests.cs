using System.Net.Http.Json;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Services.Interfaces;
using ApiGateway.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace ApiGateway.Tests.Gateway;

/// <summary>gateway-behavior.md G-30 ～ G-46：A/B 分流、排程與自動回退（不需資料庫）</summary>
[Collection(WebHostCollection.Name)]
public sealed class ExperimentRoutingIntegrationTests : IAsyncLifetime
{
    private const string Host = "api.example.com";
    private const string CookieName = ".yes.exp.orders-api";
    private const string ExperimentKey = "orders-api-ab";
    private const int Requests = 1000;

    private readonly Mock<IExperimentRuntimeRepository> _runtimeRepository = new();
    private readonly GatewayTestFactory _factory = new();
    private FakeBackend _a = null!;
    private FakeBackend _b = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _a = await FakeBackend.StartAsync("A");
        _b = await FakeBackend.StartAsync("B");
        _runtimeRepository.Setup(x => x.TryMarkAutoRolledBackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _factory.ConfigureTestServices = services =>
        {
            services.RemoveAll<IExperimentRuntimeRepository>();
            services.AddSingleton(_runtimeRepository.Object);
            services.RemoveAll<IAuditTrailService>();
            services.AddSingleton(Mock.Of<IAuditTrailService>());
            services.RemoveAll<IUnitOfWork>();
            services.AddSingleton(Mock.Of<IUnitOfWork>());
        };
        _client = _factory.CreateClient(new() { HandleCookies = false });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _a.DisposeAsync();
        await _b.DisposeAsync();
    }

    private IServiceProvider Services => _factory.Services;

    private async Task ApplyAsync(Action<YarpExperiment>? configure = null)
    {
        var f = new SnapshotFixture();
        var service = f.AddService("orders-api");
        var experiment = f.AddExperiment(service, ("A", 90, true), ("B", 10, false));
        service.DefaultExperimentId = experiment.Id;
        f.Destinations.Single(d => d.ClusterId == f.ClusterOf(experiment, "A").Id).Address = _a.Address;
        f.Destinations.Single(d => d.ClusterId == f.ClusterOf(experiment, "B").Id).Address = _b.Address;
        f.AddRoute(service, "/api/orders/{**rest}", hosts: [Host]);
        f.AddForceRule(experiment, ExperimentRuleTypes.Header, "X-Exp-Group");
        configure?.Invoke(experiment);
        await _factory.ApplyAsync(YarpConfigBuilder.Build(f.Build()));
    }

    private async Task<(string? Name, string? SetCookie, HttpResponseMessage Response)> SendAsync(string? cookie = null, string? forceHeader = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders/1");
        request.Headers.Host = Host;
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        }

        if (forceHeader is not null)
        {
            request.Headers.Add("X-Exp-Group", forceHeader);
        }

        var response = await _client.SendAsync(request);
        var name = response.IsSuccessStatusCode ? (await response.Content.ReadFromJsonAsync<FakeBackendResponse>())?.Name : null;
        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.FirstOrDefault() : null;
        return (name, setCookie, response);
    }

    [Fact]
    public async Task G30_WeightedSplit_AndStickyCookieAttributes()
    {
        await ApplyAsync();

        var results = new List<(string? Name, string? SetCookie)>();
        for (var i = 0; i < Requests; i++)
        {
            var (name, setCookie, response) = await SendAsync();
            response.Dispose();
            results.Add((name, setCookie));
        }

        var shareB = results.Count(r => r.Name == "B") * 100.0 / Requests;
        Assert.InRange(shareB, 7, 13);
        var cookie = results[0].SetCookie!;
        Assert.Contains($"{CookieName}={results[0].Name}", cookie);
        Assert.Contains("max-age=604800", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task G31_StickyCookie_AlwaysSameGroup_NoRewrite()
    {
        await ApplyAsync();

        for (var i = 0; i < 50; i++)
        {
            var (name, setCookie, response) = await SendAsync(cookie: "B");
            response.Dispose();
            Assert.Equal("B", name);
            Assert.Null(setCookie);
        }
    }

    [Fact]
    public async Task G32_G33_ForceHeader()
    {
        await ApplyAsync();

        var (forced, forcedCookie, r1) = await SendAsync(cookie: "A", forceHeader: "B");
        r1.Dispose();
        Assert.Equal("B", forced);
        Assert.Null(forcedCookie);

        var (ignored, _, r2) = await SendAsync(cookie: "A", forceHeader: "Z");
        r2.Dispose();
        Assert.Equal("A", ignored);
    }

    [Fact]
    public async Task G34_UnknownStickyGroup_Reallocates()
    {
        await ApplyAsync();

        var (_, setCookie, response) = await SendAsync(cookie: "Z");
        response.Dispose();

        Assert.NotNull(setCookie);
    }

    [Fact]
    public async Task G35_DraftExperiment_AllToControl()
    {
        await ApplyAsync(e => e.Status = ExperimentConfigStatus.Draft);

        for (var i = 0; i < 30; i++)
        {
            var (name, setCookie, response) = await SendAsync(forceHeader: "B");
            response.Dispose();
            Assert.Equal("A", name);
            Assert.Null(setCookie);
        }
    }

    [Fact]
    public async Task G50_NoGroupDisclosingHeaders()
    {
        await ApplyAsync();

        var (_, _, response) = await SendAsync(cookie: "B");
        using (response)
        {
            Assert.DoesNotContain(response.Headers, h => h.Key.Contains("Exp", StringComparison.OrdinalIgnoreCase) || h.Key.Contains("Variant", StringComparison.OrdinalIgnoreCase));
            Assert.False(response.Headers.Contains("Server"));
        }
    }

    [Fact]
    public async Task G40_G41_ScheduleByTime()
    {
        await ApplyAsync(e =>
        {
            e.StartAt = _factory.Time.GetUtcNow().AddMinutes(2);
            e.EndAt = _factory.Time.GetUtcNow().AddMinutes(10);
        });

        var (before, _, r1) = await SendAsync(cookie: "B");
        r1.Dispose();
        Assert.Equal("A", before);

        _factory.Time.Advance(TimeSpan.FromMinutes(2));
        var (running, _, r2) = await SendAsync(cookie: "B");
        r2.Dispose();
        Assert.Equal("B", running);

        _factory.Time.Advance(TimeSpan.FromMinutes(8));
        var (ended, _, r3) = await SendAsync(cookie: "B");
        r3.Dispose();
        Assert.Equal("A", ended);
    }

    private AutoRollbackMonitor CreateMonitor() => ActivatorUtilities.CreateInstance<AutoRollbackMonitor>(Services);

    private const string RollbackRule = """{"metric":"5xxRate","thresholdPercent":2,"durationMin":1,"minSamples":100}""";

    [Fact]
    public async Task G42_G46_AutoRollback_SwitchesToControl()
    {
        _b.StatusCode = 502;
        await ApplyAsync(e => e.AutoRollbackRule = RollbackRule);

        for (var i = 0; i < 120; i++)
        {
            (await SendAsync(cookie: "B")).Response.Dispose();
        }

        await CreateMonitor().EvaluateOnceAsync();

        var (name, _, response) = await SendAsync(cookie: "B");
        response.Dispose();
        Assert.Equal("A", name);
        _runtimeRepository.Verify(x => x.TryMarkAutoRolledBackAsync(ExperimentKey, It.Is<string>(m => m.Contains("\"variant\":\"B\"")), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task G43_InsufficientSamples_DoesNotTrigger()
    {
        _b.StatusCode = 500;
        await ApplyAsync(e => e.AutoRollbackRule = RollbackRule);

        for (var i = 0; i < 20; i++)
        {
            (await SendAsync(cookie: "B")).Response.Dispose();
        }

        await CreateMonitor().EvaluateOnceAsync();

        var (_, _, response) = await SendAsync(cookie: "B");
        using (response)
        {
            Assert.Equal(500, (int)response.StatusCode);
        }
    }

    [Fact]
    public async Task G44_DatabaseDown_StillSwitchesLocally()
    {
        _runtimeRepository.Setup(x => x.TryMarkAutoRolledBackAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("db down"));
        _b.StatusCode = 500;
        await ApplyAsync(e => e.AutoRollbackRule = RollbackRule);

        for (var i = 0; i < 120; i++)
        {
            (await SendAsync(cookie: "B")).Response.Dispose();
        }

        await CreateMonitor().EvaluateOnceAsync();

        var (name, _, response) = await SendAsync(cookie: "B");
        response.Dispose();
        Assert.Equal("A", name);
    }

    [Fact]
    public async Task G45_ResumeAfterRollback_SplitsAgain()
    {
        _b.StatusCode = 500;
        await ApplyAsync(e => e.AutoRollbackRule = RollbackRule);
        for (var i = 0; i < 120; i++)
        {
            (await SendAsync(cookie: "B")).Response.Dispose();
        }

        await CreateMonitor().EvaluateOnceAsync();
        _b.StatusCode = 200;

        var control = Services.GetRequiredService<IExperimentGatewayControl>();
        control.ApplyOverride(ExperimentKey, null);
        control.ResetMetrics(ExperimentKey);

        var (name, _, response) = await SendAsync(cookie: "B");
        response.Dispose();
        Assert.Equal("B", name);
    }
}
