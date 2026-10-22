using ApiGateway.Gateway.Experiments;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;

namespace ApiGateway.Services.Implementations;

/// <summary>服務頁的讀取協作元件：目前時間、生效版本發佈時間、實驗有效狀態（避免 ServiceService 依賴過多）</summary>
public sealed class ServiceReadSupport : IServiceReadSupport
{
    private readonly IConfigVersionRepository _versions;
    private readonly IExperimentStateReader _experiments;
    private readonly TimeProvider _timeProvider;

    public ServiceReadSupport(IConfigVersionRepository versions, IExperimentStateReader experiments, TimeProvider timeProvider)
    {
        _versions = versions;
        _experiments = experiments;
        _timeProvider = timeProvider;
    }

    public DateTimeOffset Now => _timeProvider.GetUtcNow();

    public async Task<DateTimeOffset?> GetActivePublishedAtAsync(CancellationToken ct = default) =>
        (await _versions.GetActiveAsync(ct))?.PublishedAt;

    public ExperimentState GetExperimentState(string experimentKey) => _experiments.Get(experimentKey);
}
