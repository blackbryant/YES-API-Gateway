namespace ApiGateway.Models.Constants;

/// <summary>多節點同步（research R3）</summary>
public static class GatewaySyncRules
{
    /// <summary>輪詢生效版本 Checksum 與執行期覆寫的間隔</summary>
    public const int PollIntervalSeconds = 5;

    /// <summary>超過此秒數未心跳視為離線</summary>
    public const int NodeStaleSeconds = 60;
}

/// <summary>發佈規則（research R8）</summary>
public static class PublishRules
{
    public const int NoteMaxLength = 500;
    public const int MaxVersionsPerDay = 99;
    public const string TimeZoneId = "Asia/Taipei";
    public const string VersionDateFormat = "yyyyMMdd";

    /// <summary>PostgreSQL lock_not_available（FOR UPDATE NOWAIT 取鎖失敗）</summary>
    public const string LockNotAvailableSqlState = "55P03";
}

/// <summary>發佈驗證 issue code；畫面文字由前端依 code 查 i18n</summary>
public static class PublishIssueCodes
{
    public const string RoutePathInvalid = "ROUTE_PATH_INVALID";
    public const string RouteHostInvalid = "ROUTE_HOST_INVALID";
    public const string RouteMethodInvalid = "ROUTE_METHOD_INVALID";
    public const string RouteOrderInvalid = "ROUTE_ORDER_INVALID";
    public const string RouteReservedPath = "ROUTE_RESERVED_PATH";
    public const string RouteRootCatchAllWithoutHost = "ROUTE_ROOT_CATCHALL_WITHOUT_HOST";
    public const string RouteConflict = "ROUTE_CONFLICT";
    public const string RouteNoTarget = "ROUTE_NO_TARGET";
    public const string TargetClusterMissing = "TARGET_CLUSTER_MISSING";
    public const string TargetClusterNoActiveHost = "TARGET_CLUSTER_NO_ACTIVE_HOST";
    public const string ExperimentWeightSum = "EXPERIMENT_WEIGHT_SUM";
    public const string ExperimentVariantCount = "EXPERIMENT_VARIANT_COUNT";
    public const string ExperimentControl = "EXPERIMENT_CONTROL";
    public const string ExperimentTimeRange = "EXPERIMENT_TIME_RANGE";
    public const string ExperimentEnded = "EXPERIMENT_ENDED";
    public const string ExperimentRollbackRule = "EXPERIMENT_ROLLBACK_RULE";

    // ---- 警告：僅提醒，不阻擋發佈 ----

    /// <summary>入口網域與某台主機位址相同（常見誤填：把後端位址填進入口網域）</summary>
    public const string RouteHostIsBackendAddress = "ROUTE_HOST_IS_BACKEND_ADDRESS";
}
