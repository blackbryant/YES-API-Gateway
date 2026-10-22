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

    /// <summary>生效版本（追蹤中，發佈時改為 History）</summary>
    Task<YarpConfigVersion?> GetActiveForUpdateAsync(CancellationToken ct = default);

    /// <summary>指定日期前綴（YYYYMMDD）的最大版本號</summary>
    Task<string?> GetMaxVersionNoOfDayAsync(string datePrefix, CancellationToken ct = default);

    /// <summary>只查生效版本的版本號與校驗碼（多節點輪詢用）</summary>
    Task<(string VersionNo, string Checksum)?> GetActiveChecksumAsync(CancellationToken ct = default);

    void Add(YarpConfigVersion version);
}

public interface IAuditLogRepository
{
    void Add(AuditLog log);
}
