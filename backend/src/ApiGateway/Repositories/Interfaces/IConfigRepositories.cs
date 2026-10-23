using ApiGateway.Models.Entities;

namespace ApiGateway.Repositories.Interfaces;

public interface IConfigDraftRepository
{
    Task<YarpConfigDraft?> GetAsync(CancellationToken ct = default);

    /// <summary>標記草稿有未發佈變更（暫存，隨 Service 的 SaveChanges 一併寫入）</summary>
    Task MarkDirtyAsync(long? userId, DateTimeOffset editedAt, CancellationToken ct = default);
}

public interface IConfigVersionRepository
{
    Task<YarpConfigVersion?> GetActiveAsync(CancellationToken ct = default);
}

public interface IAuditLogRepository
{
    void Add(AuditLog log);
}
