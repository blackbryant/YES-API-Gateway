using ApiGateway.Infrastructure.Data;
using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations.Publishing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApiGateway.Repositories.Implementations;

public sealed class ConfigSnapshotRepository : IConfigSnapshotRepository
{
    private readonly GatewayDbContext _db;

    public ConfigSnapshotRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public async Task<ConfigSnapshot> ReadAsync(CancellationToken ct = default) => new(
        await _db.Services.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
        await _db.Routes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
        await _db.RouteTransforms.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
        await _db.Clusters.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
        await _db.Destinations.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
        await _db.Experiments.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
        await _db.ExperimentVariants.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
        await _db.ExperimentRules.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct));

    public Task<YarpConfigDraft?> GetDraftAsync(CancellationToken ct = default) =>
        _db.ConfigDraft.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ConfigDraftRules.SingletonId, ct);

    public async Task<YarpConfigDraft?> TryLockDraftAsync(CancellationToken ct = default)
    {
        try
        {
            // ToListAsync 不會再包一層子查詢，FOR UPDATE NOWAIT 原樣送出
            var rows = await _db.ConfigDraft
                .FromSql($"SELECT * FROM \"YarpConfigDraft\" WHERE \"Id\" = {ConfigDraftRules.SingletonId} FOR UPDATE NOWAIT")
                .ToListAsync(ct);
            return rows.FirstOrDefault();
        }
        catch (PostgresException ex) when (ex.SqlState == PublishRules.LockNotAvailableSqlState)
        {
            return null;
        }
    }
}
