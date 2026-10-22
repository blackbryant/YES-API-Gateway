using System.Diagnostics;
using ApiGateway.Models.Constants;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace ApiGateway.Gateway.Experiments;

/// <summary>
/// YARP 代理管線第一步：路由帶有 Metadata["Yes.Experiment"] 時決定組別，
/// 非對照組以 ReassignProxyRequest 改送該組群組（research R5）。
/// 只讀記憶體中的實驗定義與執行期覆寫，不存取資料庫（憲章七）；回應後記錄計數供自動回退判斷。
/// </summary>
public sealed class ExperimentRoutingMiddleware
{
    public const string ExperimentTag = "yes.experiment";
    public const string VariantTag = "yes.variant";

    private readonly RequestDelegate _next;
    private readonly DatabaseProxyConfigProvider _provider;
    private readonly ExperimentRuntimeCache _runtime;
    private readonly ExperimentMetricsCollector _metrics;
    private readonly IProxyStateLookup _proxyState;
    private readonly TimeProvider _timeProvider;

    public ExperimentRoutingMiddleware(
        RequestDelegate next,
        DatabaseProxyConfigProvider provider,
        ExperimentRuntimeCache runtime,
        ExperimentMetricsCollector metrics,
        IProxyStateLookup proxyState,
        TimeProvider timeProvider)
    {
        _next = next;
        _provider = provider;
        _runtime = runtime;
        _metrics = metrics;
        _proxyState = proxyState;
        _timeProvider = timeProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var metadata = context.GetReverseProxyFeature().Route.Config.Metadata;
        if (metadata is null
            || !metadata.TryGetValue(ExperimentRules.RouteMetadataKey, out var experimentKey)
            || !_provider.Experiments.TryGet(experimentKey, out var experiment))
        {
            await _next(context);
            return;
        }

        var status = ExperimentStatusResolver.Resolve(experiment.Definition, _runtime.Get(experimentKey)?.OverrideStatus, _timeProvider.GetUtcNow());
        var allocation = ExperimentAllocator.Allocate(context.Request, experiment, status, Random.Shared);

        if (!allocation.Variant.IsControl && _proxyState.TryGetCluster(allocation.Variant.ClusterId, out var cluster))
        {
            context.ReassignProxyRequest(cluster);
        }

        if (allocation.WriteStickyCookie)
        {
            context.Response.Cookies.Append(experiment.Definition.Sticky.CookieName, allocation.Variant.Key, new CookieOptions
            {
                MaxAge = TimeSpan.FromDays(experiment.Definition.Sticky.Days),
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            });
        }

        // 組別只記錄在追蹤與日誌，不以回應標頭揭露給用戶端（G-50）
        Activity.Current?.SetTag(ExperimentTag, experimentKey);
        Activity.Current?.SetTag(VariantTag, allocation.Variant.Key);

        var failed = false;
        try
        {
            await _next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            // 只統計執行中的分流結果；轉送失敗（502 / 504）與例外都算 5xx
            if (status == ExperimentStatus.Running)
            {
                var isError = failed
                    || context.Features.Get<IForwarderErrorFeature>() is not null
                    || ExperimentMetricsCollector.IsServerError(context.Response.StatusCode);
                _metrics.Record(experimentKey, allocation.Variant.Key, isError);
            }
        }
    }
}
