using ApiGateway.Models.Entities;
using ApiGateway.Services.Implementations.Publishing;

namespace ApiGateway.Repositories.Interfaces;

/// <summary>發佈用：讀取所有納入版本的設定表、鎖定草稿列</summary>
public interface IConfigSnapshotRepository
{
    /// <summary>每張表各一次查詢組成快照（AsNoTracking）</summary>
    Task<ConfigSnapshot> ReadAsync(CancellationToken ct = default);

    /// <summary>草稿狀態（唯讀）</summary>
    Task<YarpConfigDraft?> GetDraftAsync(CancellationToken ct = default);

    /// <summary>
    /// 以 SELECT … FOR UPDATE NOWAIT 鎖定草稿列並回傳追蹤中的 Entity；須在交易內呼叫。
    /// 已被其他交易鎖定時回傳 null（FR-031）。
    /// </summary>
    Task<YarpConfigDraft?> TryLockDraftAsync(CancellationToken ct = default);
}
