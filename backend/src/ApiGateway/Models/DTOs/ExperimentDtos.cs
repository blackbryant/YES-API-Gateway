using System.Text.Json.Serialization;
using ApiGateway.Models.Constants;

namespace ApiGateway.Models.DTOs;

/// <summary>總覽「進行中的 A/B 實驗」（contracts/admin-api.yaml ExperimentSummary）</summary>
public sealed record ExperimentSummaryDto(
    string ExperimentKey,
    long ServiceId,
    string ServiceName,
    ExperimentStatus EffectiveStatus,
    bool IsAutoRolledBack,
    DateTimeOffset? TriggeredAt,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    IReadOnlyList<ExperimentVariantSummaryDto> Variants,
    bool HasUnhealthyHost);

public sealed record ExperimentVariantSummaryDto(string VariantKey, int Percent, bool IsControl);

/// <summary>手動暫停 / 恢復 / 結束</summary>
public sealed record ExperimentRuntimeActionRequest([property: JsonRequired] ExperimentRuntimeAction Action);

/// <summary>總覽「需要處理」項目（對應前端 AttentionItem）</summary>
public sealed record AttentionItemDto(
    string Id,
    string Type,
    string Severity,
    IReadOnlyDictionary<string, string> Params,
    string Target,
    DateTimeOffset OccurredAt);
