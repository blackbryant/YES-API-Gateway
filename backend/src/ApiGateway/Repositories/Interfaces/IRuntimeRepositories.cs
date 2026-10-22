using ApiGateway.Models.Entities;

namespace ApiGateway.Repositories.Interfaces;

/// <summary>閘道節點心跳（執行期資料，不納入設定版本）</summary>
public interface IGatewayNodeRepository
{
    /// <summary>INSERT … ON CONFLICT 更新心跳與載入版本（立即執行，不隨 SaveChanges）</summary>
    Task UpsertHeartbeatAsync(string nodeId, string? versionNo, string? checksum, DateTimeOffset now, CancellationToken ct = default);

    Task<IReadOnlyList<YarpGatewayNode>> ListAsync(CancellationToken ct = default);
}

/// <summary>A/B 實驗執行期狀態（手動操作、自動回退、排程事件；不納入設定版本）</summary>
public interface IExperimentRuntimeRepository
{
    Task<IReadOnlyList<YarpExperimentRuntime>> ListAsync(CancellationToken ct = default);

    /// <summary>追蹤中的列（手動操作用）；不存在時回傳 null</summary>
    Task<YarpExperimentRuntime?> GetForUpdateAsync(string experimentKey, CancellationToken ct = default);

    void Add(YarpExperimentRuntime runtime);

    /// <summary>
    /// 條件式寫入自動回退（僅在尚無覆寫時更新），多節點同時觸發只有一個成功；立即執行。
    /// 回傳是否由本次呼叫寫入。
    /// </summary>
    Task<bool> TryMarkAutoRolledBackAsync(string experimentKey, string metricJson, string nodeId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>條件式記錄排程事件（與上次記錄不同時才更新），確保多節點只寫一筆稽核；立即執行</summary>
    Task<bool> TryMarkScheduleEventAsync(string experimentKey, string scheduleEvent, DateTimeOffset now, CancellationToken ct = default);
}
