using System.Text.Json.Serialization;
using ApiGateway.Models.Constants;

namespace ApiGateway.Models.DTOs;

public sealed record ClusterDto(
    long Id,
    string ClusterKey,
    string DisplayName,
    string LoadBalancingPolicy,
    string HttpVersion,
    string HttpVersionPolicy,
    int ActivityTimeoutSec,
    int? MaxConnectionsPerServer,
    bool AcceptAnyServerCertificate,
    bool IsActive,
    string? Description,
    DateTimeOffset UpdatedAt);

/// <summary>新增 / 修改主機群組（ClusterKey 由系統產生 ULID，不可輸入）</summary>
public sealed record ClusterSaveRequest(
    string? DisplayName,
    string? LoadBalancingPolicy,
    string? HttpVersion,
    string? HttpVersionPolicy,
    [property: JsonRequired] int ActivityTimeoutSec,
    int? MaxConnectionsPerServer,
    [property: JsonRequired] bool AcceptAnyServerCertificate,
    [property: JsonRequired] bool IsActive,
    string? Description);

public sealed record ClusterQuery(
    string? Keyword,
    int PageIndex = PagingRules.DefaultPageIndex,
    int PageSize = PagingRules.DefaultPageSize);
