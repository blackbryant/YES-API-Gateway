using System.Globalization;
using System.Text.Json;
using ApiGateway.Gateway;
using ApiGateway.Infrastructure.Exceptions;
using ApiGateway.Models.Constants;
using ApiGateway.Models.DTOs;
using ApiGateway.Models.Entities;
using ApiGateway.Repositories.Interfaces;
using ApiGateway.Services.Implementations.Publishing;
using ApiGateway.Services.Interfaces;
using Npgsql;

namespace ApiGateway.Services.Implementations;

/// <summary>
/// 發佈（FR-025 ～ FR-031、research R8）：鎖定草稿 → 驗證 → 產生不可變版本 → commit 後才熱更新（憲章一）。
/// </summary>
public sealed class PublishService : IPublishService
{
    private const string AuditEntityType = "YarpConfigVersions";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAccessControlService _accessControl;
    private readonly IConfigSnapshotRepository _snapshots;
    private readonly IConfigVersionRepository _versions;
    private readonly IAuditTrailService _auditTrail;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishComposer _composer;
    private readonly IGatewayConfigApplier _applier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PublishService> _logger;

    public PublishService(
        IAccessControlService accessControl,
        IConfigSnapshotRepository snapshots,
        IConfigVersionRepository versions,
        IAuditTrailService auditTrail,
        IUnitOfWork unitOfWork,
        IPublishComposer composer,
        IGatewayConfigApplier applier,
        TimeProvider timeProvider,
        ILogger<PublishService> logger)
    {
        _accessControl = accessControl;
        _snapshots = snapshots;
        _versions = versions;
        _auditTrail = auditTrail;
        _unitOfWork = unitOfWork;
        _composer = composer;
        _applier = applier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>草稿的樂觀鎖權杖（LastEditedAt 的 UTC ticks）</summary>
    public static string ToDraftToken(YarpConfigDraft? draft) =>
        (draft?.LastEditedAt?.UtcTicks ?? 0).ToString(CultureInfo.InvariantCulture);

    public async Task<PublishPreviewDto> GetPreviewAsync(long? operatorId, CancellationToken ct = default)
    {
        await _accessControl.EnsureAdminAsync(operatorId, ct);

        var draft = await _snapshots.GetDraftAsync(ct);
        var snapshot = await _snapshots.ReadAsync(ct);
        var active = await _versions.GetActiveAsync(ct);
        var changes = SnapshotDiffer.Diff(active is null ? null : ConfigSnapshot.Deserialize(active.Snapshot), snapshot);
        var issues = _composer.Validate(snapshot, _timeProvider.GetUtcNow());
        var warnings = _composer.Warn(snapshot);

        return new PublishPreviewDto(active?.VersionNo, ToDraftToken(draft), changes, issues, warnings, changes.Count > 0 && issues.Count == 0);
    }

    public async Task<PublishResultDto> PublishAsync(long? operatorId, PublishRequest request, CancellationToken ct = default)
    {
        var admin = await _accessControl.EnsureAdminAsync(operatorId, ct);
        var note = request.Note?.Trim() ?? string.Empty;
        if (note.Length == 0 || note.Length > PublishRules.NoteMaxLength)
        {
            throw new ArgumentException(ErrorCodes.PublishNoteRequired);
        }

        try
        {
            var (document, version) = await PersistAsync(admin.Id, note, request.DraftToken, ct);

            // commit 成功後才切換記憶體設定；commit 失敗時閘道維持原設定（FR-029）
            _applier.ApplyDocument(document, version.VersionNo, version.Checksum);
            _logger.LogInformation("設定版本已發佈 {VersionNo} by {UserId}（changes={ChangeCount}）", version.VersionNo, admin.Id, version.ChangeCount);

            return new PublishResultDto(version.VersionNo, version.Checksum, version.PublishedAt, version.ChangeCount);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogError(ex, "發佈失敗：資料庫無法使用，閘道維持原設定");
            throw new InvalidOperationException(ErrorCodes.PublishStorageUnavailable, ex);
        }
    }

    private async Task<(YarpConfigDocument Document, YarpConfigVersion Version)> PersistAsync(
        long adminId, string note, string? draftToken, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

        var draft = await _snapshots.TryLockDraftAsync(ct) ?? throw new InvalidOperationException(ErrorCodes.PublishInProgress);
        if (!string.Equals(ToDraftToken(draft), draftToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(ErrorCodes.PublishDraftChanged);
        }

        var now = _timeProvider.GetUtcNow();
        var snapshot = await _snapshots.ReadAsync(ct);
        var active = await _versions.GetActiveForUpdateAsync(ct);
        var changes = SnapshotDiffer.Diff(active is null ? null : ConfigSnapshot.Deserialize(active.Snapshot), snapshot);
        if (changes.Count == 0)
        {
            throw new ArgumentException(ErrorCodes.PublishNoChanges);
        }

        var composition = await _composer.ComposeAsync(snapshot, now, ct);
        if (composition.Issues.Count > 0 || composition.Document is null || composition.Checksum is null)
        {
            throw new PublishValidationException(composition.Issues);
        }

        var maxOfDay = await _versions.GetMaxVersionNoOfDayAsync(VersionNumberGenerator.DatePrefix(now, PublishRules.TimeZoneId), ct);
        var version = new YarpConfigVersion
        {
            VersionNo = VersionNumberGenerator.Next(now, PublishRules.TimeZoneId, maxOfDay),
            Status = ConfigVersionStatus.Active,
            BasedOnVersionNo = active?.VersionNo,
            Snapshot = snapshot.Serialize(),
            YarpConfig = YarpConfigBuilder.Serialize(composition.Document),
            Checksum = composition.Checksum,
            ChangeSummary = JsonSerializer.Serialize(changes, SerializerOptions),
            ChangeCount = changes.Count,
            Note = note,
            PublishedBy = adminId,
            PublishedAt = now,
        };

        // 同一時間只能有一筆 Active（部分唯一索引）：先把舊版改為 History 再新增
        if (active is not null)
        {
            active.Status = ConfigVersionStatus.History;
            await _unitOfWork.SaveChangesAsync(ct);
        }

        _versions.Add(version);
        draft.IsDirty = false;
        draft.ChangeCount = 0;
        draft.BaseVersionNo = version.VersionNo;
        _auditTrail.Record(AuditActions.Publish, AuditEntityType, version.VersionNo, adminId,
            after: new { version.VersionNo, version.Note, version.ChangeCount, version.Checksum, version.BasedOnVersionNo });
        await _unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (composition.Document, version);
    }

    private static bool IsStorageFailure(Exception ex) =>
        ex is NpgsqlException or TimeoutException
        || ex.InnerException is NpgsqlException or TimeoutException;
}
