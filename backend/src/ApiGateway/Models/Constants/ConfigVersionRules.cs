namespace ApiGateway.Models.Constants;

/// <summary>設定版本狀態（YarpConfigVersions.Status）</summary>
public static class ConfigVersionStatus
{
    public const string Active = "Active";
    public const string History = "History";
    public const string RolledBack = "RolledBack";
}

/// <summary>草稿狀態表規則（YarpConfigDraft）</summary>
public static class ConfigDraftRules
{
    /// <summary>草稿狀態表固定只有一列</summary>
    public const short SingletonId = 1;

    /// <summary>設定版本最多保留數（FR-13-02）</summary>
    public const int MaxRetainedVersions = 20;
}
