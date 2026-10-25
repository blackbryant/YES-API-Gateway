namespace ApiGateway.Models.DTOs;

/// <summary>發佈驗證錯誤或警告（Code 見 PublishIssueCodes；畫面文字由前端依 Code + Params 組字）</summary>
public sealed record ValidationIssue(
    string Code,
    long? ServiceId,
    string? ServiceName,
    string? RouteKey,
    string? Field,
    IReadOnlyDictionary<string, string>? Params);

/// <summary>變更項目（與生效版本比對）</summary>
public sealed record ChangeItem(
    ChangeEntityType EntityType,
    string EntityKey,
    long? ServiceId,
    string? ServiceName,
    ChangeAction Action);

public enum ChangeEntityType
{
    Service,
    Route,
    Experiment,
    Cluster,
    Host,
}

public enum ChangeAction
{
    Added,
    Modified,
    Removed,
}

public sealed record PublishRequest(string? Note, string? DraftToken);

public sealed record PublishPreviewDto(
    string? BaseVersion,
    string DraftToken,
    IReadOnlyList<ChangeItem> Changes,
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<ValidationIssue> Warnings,
    bool CanPublish);

public sealed record PublishResultDto(string VersionNo, string Checksum, DateTimeOffset PublishedAt, int ChangeCount);
