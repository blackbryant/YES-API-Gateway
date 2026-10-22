using ApiGateway.Infrastructure.Data;
using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Repositories.Implementations;

public sealed class ConfigDraftRepository : IConfigDraftRepository
{
    private readonly GatewayDbContext _db;

    public ConfigDraftRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public Task<YarpConfigDraft?> GetAsync(CancellationToken ct = default) =>
        _db.ConfigDraft.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ConfigDraftRules.SingletonId, ct);

    public async Task MarkDirtyAsync(long? userId, DateTimeOffset editedAt, CancellationToken ct = default)
    {
        var draft = await _db.ConfigDraft.FirstOrDefaultAsync(x => x.Id == ConfigDraftRules.SingletonId, ct);
        if (draft is null)
        {
            draft = new YarpConfigDraft { Id = ConfigDraftRules.SingletonId };
            _db.ConfigDraft.Add(draft);
        }

        draft.IsDirty = true;
        draft.ChangeCount += 1;
        draft.LastEditedBy = userId;
        draft.LastEditedAt = editedAt;
    }
}

public sealed class ConfigVersionRepository : IConfigVersionRepository
{
    private readonly GatewayDbContext _db;

    public ConfigVersionRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public Task<YarpConfigVersion?> GetActiveAsync(CancellationToken ct = default) =>
        _db.ConfigVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Status == ConfigVersionStatus.Active, ct);
}

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly GatewayDbContext _db;

    public AuditLogRepository(GatewayDbContext db)
    {
        _db = db;
    }

    public void Add(AuditLog log) => _db.AuditLogs.Add(log);
}
