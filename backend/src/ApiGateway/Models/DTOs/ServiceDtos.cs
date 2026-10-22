using System.Text.Json.Serialization;
using ApiGateway.Models.Constants;

namespace ApiGateway.Models.DTOs;

/// <summary>服務的分流方式</summary>
public enum RoutingMode
{
    Single,
    AbTest,
}

/// <summary>新增 / 修改服務（整份覆寫：入口、主機、分流設定一併同步）</summary>
public sealed record ServiceSaveRequest(
    string? ServiceKey,
    string? DisplayName,
    string[]? Tags,
    [property: JsonRequired] bool IsActive,
    string? Description,
    List<RouteInput>? Routes,
    [property: JsonRequired] RoutingMode RoutingMode,
    List<HostInput>? Hosts,
    ExperimentInput? Experiment);

public sealed record RouteInput(
    long? Id,
    string? MatchPath,
    string[]? MatchHosts,
    string[]? MatchMethods,
    int? Order,
    string? PathRemovePrefix,
    [property: JsonRequired] bool IsActive,
    string? Description);

public sealed record HostInput(long? Id, string? Address, [property: JsonRequired] bool IsActive);

public sealed record ExperimentInput(
    [property: JsonRequired] bool Enabled,
    string? StickyCookieName,
    [property: JsonRequired] int StickyDays,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    AutoRollbackRuleDto? AutoRollback,
    List<VariantInput>? Variants,
    List<ForceRuleInput>? ForceRules);

public sealed record VariantInput(
    string? VariantKey,
    [property: JsonRequired] int WeightPercent,
    [property: JsonRequired] bool IsControl,
    List<HostInput>? Hosts);

public sealed record ForceRuleInput(string? MatchType, string? MatchName, string? MatchValue, string? VariantKey);

/// <summary>自動回退條件；未提供的欄位套用 ExperimentRules 預設值</summary>
public sealed record AutoRollbackRuleDto(string? Metric, decimal? ThresholdPercent, int? DurationMin, int? MinSamples);

public sealed record ServiceQuery(
    string? Keyword,
    string? Tag,
    int PageIndex = PagingRules.DefaultPageIndex,
    int PageSize = PagingRules.DefaultPageSize);

public sealed record ServiceActiveRequest([property: JsonRequired] bool IsActive);

// ---- 回應 ----

public sealed record ServiceSummaryDto(
    long Id,
    string ServiceKey,
    string DisplayName,
    string[] Tags,
    bool IsActive,
    int RouteCount,
    string[] Hosts,
    RoutingMode RoutingMode,
    bool HasUnpublishedChanges,
    DateTimeOffset UpdatedAt);

public sealed record ServiceDetailDto(
    long Id,
    string ServiceKey,
    string DisplayName,
    string[] Tags,
    bool IsActive,
    string? Description,
    IReadOnlyList<RouteDetailDto> Routes,
    RoutingMode RoutingMode,
    ClusterInfoDto? Cluster,
    IReadOnlyList<HostDto> Hosts,
    ExperimentDetailDto? Experiment,
    DateTimeOffset UpdatedAt);

public sealed record RouteDetailDto(
    long Id,
    string RouteKey,
    string MatchPath,
    string[] MatchHosts,
    string[] MatchMethods,
    int Order,
    string? PathRemovePrefix,
    bool IsActive,
    string? Description);

public sealed record HostDto(long Id, string Address, bool IsActive);

public sealed record ClusterInfoDto(string ClusterKey, string DisplayName);

public sealed record ExperimentDetailDto(
    string ExperimentKey,
    bool Enabled,
    string StickyCookieName,
    int StickyDays,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    AutoRollbackRuleDto? AutoRollback,
    IReadOnlyList<VariantDetailDto> Variants,
    IReadOnlyList<ForceRuleInput> ForceRules,
    ExperimentStatus EffectiveStatus,
    bool IsAutoRolledBack);

public sealed record VariantDetailDto(string VariantKey, int WeightPercent, bool IsControl, ClusterInfoDto Cluster, IReadOnlyList<HostDto> Hosts);
