using ApiGateway.Infrastructure.Data;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Repositories.Implementations;

public sealed class ClusterRepository : IClusterRepository
{
    private readonly GatewayDbContext _db;

    public ClusterRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<YarpCluster> Items, int TotalCount)> SearchAsync(
        string? keyword, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.Clusters.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{SqlLike.Escape(keyword.Trim())}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.ClusterKey, pattern, SqlLike.EscapeCharacter) ||
                (x.Description != null && EF.Functions.ILike(x.Description, pattern, SqlLike.EscapeCharacter)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.ClusterKey)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<YarpCluster?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _db.Clusters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<YarpCluster?> GetForUpdateAsync(long id, CancellationToken ct = default) =>
        _db.Clusters.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistsByKeyAsync(string clusterKey, long? excludeId, CancellationToken ct = default) =>
        _db.Clusters.AnyAsync(x => x.ClusterKey == clusterKey && (excludeId == null || x.Id != excludeId), ct);

    public async Task<bool> IsInUseAsync(long id, CancellationToken ct = default) =>
        await _db.Destinations.AnyAsync(x => x.ClusterId == id, ct)
        || await _db.Services.AnyAsync(x => x.DefaultClusterId == id, ct)
        || await _db.Routes.AnyAsync(x => x.ClusterId == id, ct)
        || await _db.ExperimentVariants.AnyAsync(x => x.ClusterId == id, ct)
        || await _db.ApiVersions.AnyAsync(x => x.ClusterId == id, ct);

    public void Add(YarpCluster cluster) => _db.Clusters.Add(cluster);

    public void Remove(YarpCluster cluster) => _db.Clusters.Remove(cluster);
}
