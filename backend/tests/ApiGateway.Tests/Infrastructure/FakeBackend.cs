using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Tests.Infrastructure;

/// <summary>測試用假後端：以 Kestrel 監聽隨機埠，回應 { name, path, query }</summary>
public sealed class FakeBackend : IAsyncDisposable
{
    private readonly WebApplication _app;

    private FakeBackend(string name, WebApplication app, string address)
    {
        Name = name;
        _app = app;
        Address = address;
    }

    public string Name { get; }

    /// <summary>如 http://127.0.0.1:54321</summary>
    public string Address { get; }

    /// <summary>固定回應狀態碼（預設 200）</summary>
    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    /// <summary>回應前延遲</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public int RequestCount;

    public static async Task<FakeBackend> StartAsync(string name)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        FakeBackend? backend = null;
        app.Run(async context =>
        {
            var self = backend!;
            Interlocked.Increment(ref self.RequestCount);
            if (self.Delay > TimeSpan.Zero)
            {
                await Task.Delay(self.Delay, context.RequestAborted);
            }

            context.Response.StatusCode = self.StatusCode;
            await context.Response.WriteAsJsonAsync(new
            {
                name = self.Name,
                path = context.Request.Path.Value,
                query = context.Request.QueryString.Value,
            });
        });

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        backend = new FakeBackend(name, app, address);
        return backend;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

/// <summary>假後端回應內容</summary>
public sealed record FakeBackendResponse(string Name, string Path, string? Query);
