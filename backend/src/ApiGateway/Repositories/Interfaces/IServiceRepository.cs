using ApiGateway.Models.Entities;

namespace ApiGateway.Repositories.Interfaces;

/// <summary>服務聚合：服務 + 入口 + 轉換 + 群組 / 主機 + A/B 實驗（Entity 無導覽屬性，關聯以 Id 對應）</summary>
public sealed record ServiceAggregate(
    YarpService Service,
    List<YarpRoute> Routes,
    List<YarpRouteTransform> Transforms,
    List<YarpCluster> Clusters,
    List<YarpDestination> Destinations,
    YarpExperiment? Experiment,
    List<YarpExperimentVariant> Variants,
    List<YarpExperimentRule> Rules)
{
    /// <summary>同步時可能新建或移除實驗</summary>
    public YarpExperiment? Experiment { get; set; } = Experiment;
}

/// <summary>實驗與所屬服務</summary>
public sealed record ExperimentOwner(string ExperimentKey, long ServiceId, string ServiceName);

/// <summary>服務列表單列（含入口數與網域彙整）</summary>
public sealed record ServiceListRow(YarpService Service, int RouteCount, string[] Hosts);

/// <summary>
/// 服務聚合的資料存取（取代分散的 Route / Experiment Repository，避免 Service 依賴過多）。
/// 只暫存異動，由 Service 透過 IUnitOfWork 提交。
/// </summary>
public interface IServiceRepository
{
    Task<(IReadOnlyList<ServiceListRow> Items, int TotalCount)> SearchAsync(string? keyword, string? tag, int skip, int take, CancellationToken ct = default);

    Task<bool> ExistsByKeyAsync(string serviceKey, long? excludeId, CancellationToken ct = default);

    /// <summary>取得服務聚合；tracking = true 時回傳追蹤中的 Entity（修改用）</summary>
    Task<ServiceAggregate?> GetAggregateAsync(long id, bool tracking, CancellationToken ct = default);

    /// <summary>群組是否被其他服務（預設導向、入口、A/B 組別、API 版本）引用</summary>
    Task<bool> IsClusterReferencedOutsideAsync(IReadOnlyCollection<long> clusterIds, long serviceId, CancellationToken ct = default);

    /// <summary>實驗所屬服務（依 ExperimentKey），供實驗摘要顯示服務名稱</summary>
    Task<IReadOnlyList<ExperimentOwner>> GetExperimentOwnersAsync(IReadOnlyCollection<string> experimentKeys, CancellationToken ct = default);

    void Add(object entity);

    void Remove(object entity);
}
