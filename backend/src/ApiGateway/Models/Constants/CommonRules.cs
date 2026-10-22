namespace ApiGateway.Models.Constants;

/// <summary>分頁規則</summary>
public static class PagingRules
{
    public const int DefaultPageIndex = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

/// <summary>Kestrel 全域限制（憲章七：請求大小上限預設 30 MB）</summary>
public static class GatewayLimits
{
    public const long MaxRequestBodySizeBytes = 30L * 1024 * 1024;
}

/// <summary>稽核動作（AuditLogs.Action）</summary>
public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Publish = "Publish";
    public const string Rollback = "Rollback";
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
}

/// <summary>系統內建策略名稱（憲章二：不得刪除）</summary>
public static class BuiltInPolicyNames
{
    public const string Default = "default";
    public const string Anonymous = "anonymous";
    public const string Disable = "disable";
}
