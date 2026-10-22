using ApiGateway.Infrastructure.Data;
using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Repositories.Implementations;

public sealed class GatewayNodeRepository : IGatewayNodeRepository
{
    private readonly GatewayDbContext _db;

    public GatewayNodeRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public Task UpsertHeartbeatAsync(string nodeId, string? versionNo, string? checksum, DateTimeOffset now, CancellationToken ct = default) =>
        _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "YarpGatewayNodes" ("NodeId", "LoadedVersionNo", "LoadedChecksum", "LastHeartbeatAt")
            VALUES ({nodeId}, {versionNo}, {checksum}, {now})
            ON CONFLICT ("NodeId") DO UPDATE SET
                "LoadedVersionNo" = EXCLUDED."LoadedVersionNo",
                "LoadedChecksum" = EXCLUDED."LoadedChecksum",
                "LastHeartbeatAt" = EXCLUDED."LastHeartbeatAt"
            """,
            ct);

    public async Task<IReadOnlyList<YarpGatewayNode>> ListAsync(CancellationToken ct = default) =>
        await _db.GatewayNodes.AsNoTracking().OrderBy(x => x.NodeId).ToListAsync(ct);
}

public sealed class ExperimentRuntimeRepository : IExperimentRuntimeRepository
{
    private readonly GatewayDbContext _db;

    public ExperimentRuntimeRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<YarpExperimentRuntime>> ListAsync(CancellationToken ct = default) =>
        await _db.ExperimentRuntime.AsNoTracking().ToListAsync(ct);

    public Task<YarpExperimentRuntime?> GetForUpdateAsync(string experimentKey, CancellationToken ct = default) =>
        _db.ExperimentRuntime.FirstOrDefaultAsync(x => x.ExperimentKey == experimentKey, ct);

    public void Add(YarpExperimentRuntime runtime) => _db.ExperimentRuntime.Add(runtime);

    public async Task<bool> TryMarkAutoRolledBackAsync(string experimentKey, string metricJson, string nodeId, DateTimeOffset now, CancellationToken ct = default)
    {
        var paused = ExperimentOverrideStatus.Paused;
        var affected = await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "YarpExperimentRuntime"
                ("ExperimentKey", "OverrideStatus", "IsAutoRolledBack", "TriggeredMetric", "TriggeredAt", "TriggeredByNode", "UpdatedAt")
            VALUES ({experimentKey}, {paused}, TRUE, CAST({metricJson} AS jsonb), {now}, {nodeId}, {now})
            ON CONFLICT ("ExperimentKey") DO UPDATE SET
                "OverrideStatus" = EXCLUDED."OverrideStatus",
                "IsAutoRolledBack" = TRUE,
                "TriggeredMetric" = EXCLUDED."TriggeredMetric",
                "TriggeredAt" = EXCLUDED."TriggeredAt",
                "TriggeredByNode" = EXCLUDED."TriggeredByNode",
                "UpdatedBy" = NULL,
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            WHERE "YarpExperimentRuntime"."OverrideStatus" IS NULL
            """,
            ct);
        return affected > 0;
    }

    public async Task<bool> TryMarkScheduleEventAsync(string experimentKey, string scheduleEvent, DateTimeOffset now, CancellationToken ct = default)
    {
        var affected = await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "YarpExperimentRuntime" ("ExperimentKey", "IsAutoRolledBack", "LastScheduleEvent", "UpdatedAt")
            VALUES ({experimentKey}, FALSE, {scheduleEvent}, {now})
            ON CONFLICT ("ExperimentKey") DO UPDATE SET
                "LastScheduleEvent" = EXCLUDED."LastScheduleEvent",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            WHERE "YarpExperimentRuntime"."LastScheduleEvent" IS DISTINCT FROM EXCLUDED."LastScheduleEvent"
            """,
            ct);
        return affected > 0;
    }
}
