namespace ApiGateway.Models.Constants;

/// <summary>服務（YarpServices）與入口路由（YarpRoutes）欄位規則</summary>
public static class RouteRules
{
    public const int MinOrder = 0;
    public const int MaxOrder = 10000;
    public const int DefaultOrder = 100;

    /// <summary>系統端點（健康檢查、Admin API）的路由順序，優先於所有動態路由</summary>
    public const int SystemEndpointOrder = int.MinValue + 1000;

    public const int MatchPathMaxLength = 255;
    public const int MaxHosts = 20;

    public const string ServiceKeyPattern = "^[a-z0-9][a-z0-9-]{1,62}$";
    public const int DisplayNameMaxLength = 100;
    public const int TagMaxLength = 30;
    public const int MaxTags = 10;
    public const int DescriptionMaxLength = 500;
    public const int AddressMaxLength = 255;

    /// <summary>網域：host、*.host、host:port</summary>
    public const string HostPattern = @"^(\*\.)?([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*(:\d{1,5})?$";

    public const string TransformPathRemovePrefix = "PathRemovePrefix";
    public const string TransformDirectionRequest = "Request";

    /// <summary>主機 DestinationKey 前綴（d-{ULID}）</summary>
    public const string DestinationKeyPrefix = "d-";

    public static readonly IReadOnlySet<string> AllowedMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH",
    };
}
