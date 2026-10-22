using ApiGateway.Gateway;
using ApiGateway.Models.DTOs;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Services.Implementations.Publishing;

/// <summary>驗證結果 + 產生的 YARP 設定（有驗證錯誤時 Document / Checksum 為 null）</summary>
public sealed record PublishComposition(IReadOnlyList<ValidationIssue> Issues, YarpConfigDocument? Document, string? Checksum);

/// <summary>發佈的協作元件：本系統規則驗證 → 產生 YARP 設定 → 以 YARP 內建驗證器雙重檢查</summary>
public interface IPublishComposer
{
    Task<PublishComposition> ComposeAsync(ConfigSnapshot snapshot, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>只執行本系統規則驗證（發佈預覽使用）</summary>
    IReadOnlyList<ValidationIssue> Validate(ConfigSnapshot snapshot, DateTimeOffset now);
}

public sealed class PublishComposer : IPublishComposer
{
    public const string YarpConfigInvalidCode = "YARP_CONFIG_INVALID";

    private readonly IConfigValidator _yarpValidator;
    private readonly GatewayOptions _options;

    public PublishComposer(IConfigValidator yarpValidator, IOptions<GatewayOptions> options)
    {
        _yarpValidator = yarpValidator;
        _options = options.Value;
    }

    public IReadOnlyList<ValidationIssue> Validate(ConfigSnapshot snapshot, DateTimeOffset now) =>
        PublishValidator.Validate(snapshot, _options, now);

    public async Task<PublishComposition> ComposeAsync(ConfigSnapshot snapshot, DateTimeOffset now, CancellationToken ct = default)
    {
        var issues = Validate(snapshot, now);
        if (issues.Count > 0)
        {
            return new PublishComposition(issues, null, null);
        }

        var document = YarpConfigBuilder.Build(snapshot);
        var yarpIssues = new List<ValidationIssue>();
        foreach (var route in document.Routes)
        {
            foreach (var error in await _yarpValidator.ValidateRouteAsync(route))
            {
                yarpIssues.Add(new ValidationIssue(YarpConfigInvalidCode, null, null, route.RouteId, null, new Dictionary<string, string> { ["message"] = error.Message }));
            }
        }

        foreach (var cluster in document.Clusters)
        {
            foreach (var error in await _yarpValidator.ValidateClusterAsync(cluster))
            {
                yarpIssues.Add(new ValidationIssue(YarpConfigInvalidCode, null, null, null, nameof(ClusterConfig), new Dictionary<string, string> { ["cluster"] = cluster.ClusterId, ["message"] = error.Message }));
            }
        }

        return yarpIssues.Count > 0
            ? new PublishComposition(yarpIssues, null, null)
            : new PublishComposition([], document, YarpConfigBuilder.ComputeChecksum(document));
    }
}
