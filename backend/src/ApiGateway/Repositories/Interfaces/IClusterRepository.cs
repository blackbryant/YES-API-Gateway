using ApiGateway.Models.Entities;

namespace ApiGateway.Repositories.Interfaces;

public interface IClusterRepository
{
    Task<(IReadOnlyList<YarpCluster> Items, int TotalCount)> SearchAsync(string? keyword, int skip, int take, CancellationToken ct = default);

    Task<YarpCluster?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<YarpCluster?> GetForUpdateAsync(long id, CancellationToken ct = default);

    Task<bool> ExistsByKeyAsync(string clusterKey, long? excludeId, CancellationToken ct = default);

    /// <summary>是否仍被服務、路由、A/B 變體、API 版本或主機引用</summary>
    Task<bool> IsInUseAsync(long id, CancellationToken ct = default);

    void Add(YarpCluster cluster);

    void Remove(YarpCluster cluster);
}
