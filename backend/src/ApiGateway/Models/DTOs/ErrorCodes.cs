namespace ApiGateway.Models.DTOs;

/// <summary>API 錯誤碼，命名 &lt;模組複數&gt;_&lt;情境&gt;；畫面文字由前端依錯誤碼查 i18n。</summary>
public static class ErrorCodes
{
    // 共用
    public const string ValidationFailed = "COMMON_VALIDATION_FAILED";
    public const string PagingInvalid = "COMMON_PAGING_INVALID";
    public const string NotFound = "COMMON_NOT_FOUND";
    public const string Forbidden = "COMMON_FORBIDDEN";
    public const string InternalError = "COMMON_INTERNAL_ERROR";

    // 認證
    public const string AuthCredentialsRequired = "AUTH_CREDENTIALS_REQUIRED";
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthAccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AuthUnauthenticated = "AUTH_UNAUTHENTICATED";
    public const string AuthAccessTokenInvalid = "AUTH_ACCESS_TOKEN_INVALID";

    // 主機群組
    public const string ClustersNotFound = "CLUSTERS_NOT_FOUND";
    public const string ClustersKeyInvalid = "CLUSTERS_KEY_INVALID";
    public const string ClustersKeyExists = "CLUSTERS_KEY_EXISTS";
    public const string ClustersLoadBalancingPolicyInvalid = "CLUSTERS_LOAD_BALANCING_POLICY_INVALID";
    public const string ClustersHttpVersionInvalid = "CLUSTERS_HTTP_VERSION_INVALID";
    public const string ClustersHttpVersionPolicyInvalid = "CLUSTERS_HTTP_VERSION_POLICY_INVALID";
    public const string ClustersActivityTimeoutInvalid = "CLUSTERS_ACTIVITY_TIMEOUT_INVALID";
    public const string ClustersMaxConnectionsInvalid = "CLUSTERS_MAX_CONNECTIONS_INVALID";
    public const string ClustersDescriptionTooLong = "CLUSTERS_DESCRIPTION_TOO_LONG";
    public const string ClustersInUse = "CLUSTERS_IN_USE";
}
