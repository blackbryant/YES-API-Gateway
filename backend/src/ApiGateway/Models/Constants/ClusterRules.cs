using Yarp.ReverseProxy.LoadBalancing;

namespace ApiGateway.Models.Constants;

/// <summary>主機群組（YarpClusters）欄位規則</summary>
public static class ClusterRules
{
    public const int KeyMaxLength = 100;

    /// <summary>ClusterKey 為系統產生的 ULID（26 字元 Crockford Base32）</summary>
    public const string KeyPattern = "^[0-9A-HJKMNP-TV-Z]{26}$";

    public const int DisplayNameMaxLength = 100;

    public const int DescriptionMaxLength = 500;

    /// <summary>後端無回應即中斷的預設秒數（YARP 預設 100 秒）</summary>
    public const int DefaultActivityTimeoutSec = 100;

    public const int MinActivityTimeoutSec = 1;
    public const int MaxActivityTimeoutSec = 3600;

    public static readonly string DefaultLoadBalancingPolicy = LoadBalancingPolicies.PowerOfTwoChoices;
    public const string DefaultHttpVersion = "2.0";
    public const string DefaultHttpVersionPolicy = "RequestVersionOrLower";

    public static readonly IReadOnlySet<string> LoadBalancingPolicyOptions = new HashSet<string>(StringComparer.Ordinal)
    {
        LoadBalancingPolicies.PowerOfTwoChoices,
        LoadBalancingPolicies.RoundRobin,
        LoadBalancingPolicies.LeastRequests,
        LoadBalancingPolicies.Random,
        LoadBalancingPolicies.FirstAlphabetical,
    };

    public static readonly IReadOnlySet<string> HttpVersionOptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "1.1", "2.0", "3.0",
    };

    public static readonly IReadOnlySet<string> HttpVersionPolicyOptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "RequestVersionOrLower", "RequestVersionOrHigher", "RequestVersionExact",
    };
}
