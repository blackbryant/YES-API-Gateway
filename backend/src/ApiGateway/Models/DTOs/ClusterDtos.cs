using System.Text.Json.Serialization;
using ApiGateway.Models.Constants;

namespace ApiGateway.Models.DTOs;

public sealed record ClusterDto(
    long Id,
    string ClusterKey,
    string LoadBalancingPolicy,
    string HttpVersion,
    string HttpVersionPolicy,
    int ActivityTimeoutSec,
    int? MaxConnectionsPerServer,
    bool AcceptAnyServerCertificate,
    bool IsActive,
    string? Description,
    DateTimeOffset UpdatedAt);

/// <summary>新增 / 修改主機群組</summary>
public sealed record ClusterSaveRequest(
    string? ClusterKey,
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
