using System.Collections.Concurrent;
using ApiGateway.Models.Constants;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Interfaces;

namespace ApiGateway.Gateway.Experiments;

/// <summary>
/// 排程事件稽核（FR-019d）：排程本身由各節點依時間判斷、不需寫資料庫；
/// 此服務每分鐘偵測「開始執行」與「到期結束」，以條件式更新確保多節點只寫一筆稽核（操作者為系統）。
/// </summary>
public sealed class ExperimentScheduleAuditService : BackgroundService
{
    private const string AuditEntityType = "YarpExperimentRuntime";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseProxyConfigProvider _provider;
    private readonly ExperimentRuntimeCache _runtime;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExperimentScheduleAuditService> _logger;
    private readonly ConcurrentDictionary<string, string> _recorded = new(StringComparer.Ordinal);

    public ExperimentScheduleAuditService(
        IServiceScopeFactory scopeFactory,
        DatabaseProxyConfigProvider provider,
        ExperimentRuntimeCache runtime,
        TimeProvider timeProvider,
        ILogger<ExperimentScheduleAuditService> logger)
    {
        _scopeFactory = scopeFactory;
        _provider = provider;
        _runtime = runtime;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(ExperimentRules.ScheduleAuditIntervalSeconds), _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckOnceAsync(stoppingToken);
        }
    }

    /// <summary>檢查一輪（供測試直接呼叫）</summary>
    public async Task CheckOnceAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var definition in _provider.Experiments.All.Select(e => e.Definition))
        {
            var status = ExperimentStatusResolver.Resolve(definition, _runtime.Get(definition.Key)?.OverrideStatus, now);
            var scheduleEvent = status switch
            {
                ExperimentStatus.Running when definition.StartAt is { } start && now >= start => ExperimentScheduleEvents.Started,
                ExperimentStatus.Ended when definition.EndAt is { } end && now >= end => ExperimentScheduleEvents.Ended,
                _ => null,
            };

            if (scheduleEvent is null || (_recorded.TryGetValue(definition.Key, out var last) && last == scheduleEvent))
            {
                continue;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IExperimentRuntimeRepository>();
                if (await repository.TryMarkScheduleEventAsync(definition.Key, scheduleEvent, now, ct))
                {
                    var action = scheduleEvent == ExperimentScheduleEvents.Started ? AuditActions.ExperimentScheduleStarted : AuditActions.ExperimentScheduleEnded;
                    scope.ServiceProvider.GetRequiredService<IAuditTrailService>()
                        .Record(action, AuditEntityType, definition.Key, null, after: new { definition.StartAt, definition.EndAt });
                    await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
                    _logger.LogInformation("A/B 實驗 {ExperimentKey} 排程事件 {Event}", definition.Key, scheduleEvent);
                }

                _recorded[definition.Key] = scheduleEvent;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "排程事件稽核寫入失敗，下一輪重試 {ExperimentKey}", definition.Key);
            }
        }
    }
}
