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
    public const string ClustersDisplayNameInvalid = "CLUSTERS_DISPLAY_NAME_INVALID";

    // 服務、入口、主機
    public const string ServicesNotFound = "SERVICES_NOT_FOUND";
    public const string ServicesKeyInvalid = "SERVICES_KEY_INVALID";
    public const string ServicesKeyExists = "SERVICES_KEY_EXISTS";
    public const string ServicesKeyImmutable = "SERVICES_KEY_IMMUTABLE";
    public const string ServicesDisplayNameInvalid = "SERVICES_DISPLAY_NAME_INVALID";
    public const string ServicesTagsInvalid = "SERVICES_TAGS_INVALID";
    public const string ServicesDescriptionTooLong = "SERVICES_DESCRIPTION_TOO_LONG";
    public const string ServicesRoutesRequired = "SERVICES_ROUTES_REQUIRED";
    public const string ServicesHostsRequired = "SERVICES_HOSTS_REQUIRED";
    public const string ServicesRoutingModeInvalid = "SERVICES_ROUTING_MODE_INVALID";
    public const string RoutesNotFound = "ROUTES_NOT_FOUND";
    public const string RoutesPathInvalid = "ROUTES_PATH_INVALID";
    public const string RoutesHostInvalid = "ROUTES_HOST_INVALID";
    public const string RoutesMethodInvalid = "ROUTES_METHOD_INVALID";
    public const string RoutesOrderInvalid = "ROUTES_ORDER_INVALID";
    public const string RoutesPrefixInvalid = "ROUTES_PREFIX_INVALID";
    public const string HostsAddressInvalid = "HOSTS_ADDRESS_INVALID";
    public const string HostsNotFound = "HOSTS_NOT_FOUND";

    // A/B 實驗
    public const string ExperimentsInvalid = "EXPERIMENTS_INVALID";
    public const string ExperimentsNotFound = "EXPERIMENTS_NOT_FOUND";
    public const string ExperimentsActionInvalid = "EXPERIMENTS_ACTION_INVALID";

    // 發佈
    public const string PublishNoteRequired = "PUBLISH_NOTE_REQUIRED";
    public const string PublishNoChanges = "PUBLISH_NO_CHANGES";
    public const string PublishValidationFailed = "PUBLISH_VALIDATION_FAILED";
    public const string PublishInProgress = "PUBLISH_IN_PROGRESS";
    public const string PublishDraftChanged = "PUBLISH_DRAFT_CHANGED";
    public const string PublishDailyLimit = "PUBLISH_DAILY_LIMIT";
    public const string PublishStorageUnavailable = "PUBLISH_STORAGE_UNAVAILABLE";
}
