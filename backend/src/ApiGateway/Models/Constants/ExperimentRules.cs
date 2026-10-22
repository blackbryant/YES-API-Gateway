namespace ApiGateway.Models.Constants;

/// <summary>A/B 實驗的設定狀態（YarpExperiments.Status，納入版本）</summary>
public static class ExperimentConfigStatus
{
    public const string Draft = "Draft";
    public const string Running = "Running";
}

/// <summary>實驗有效狀態：由設定 + 執行期覆寫 + 時間計算</summary>
public enum ExperimentStatus
{
    Draft,
    Scheduled,
    Running,
    Paused,
    Ended,
}

/// <summary>執行期覆寫狀態（YarpExperimentRuntime.OverrideStatus）</summary>
public static class ExperimentOverrideStatus
{
    public const string Paused = "Paused";
    public const string Ended = "Ended";
}

/// <summary>排程事件（YarpExperimentRuntime.LastScheduleEvent）</summary>
public static class ExperimentScheduleEvents
{
    public const string Started = "Started";
    public const string Ended = "Ended";
}

/// <summary>手動操作（POST experiments/{key}/runtime-actions）</summary>
public enum ExperimentRuntimeAction
{
    Pause,
    Resume,
    End,
}

/// <summary>分配規則（YarpExperimentRules）</summary>
public static class ExperimentRuleTypes
{
    public const string Header = "Header";
    public const string Cookie = "Cookie";
    public const string Query = "Query";
    public const string ForceVariant = "ForceVariant";

    public static readonly IReadOnlySet<string> MatchTypes = new HashSet<string>(StringComparer.Ordinal) { Header, Cookie, Query };
}

/// <summary>A/B 實驗規則與預設值</summary>
public static class ExperimentRules
{
    public const int MinVariants = 2;
    public const int MaxVariants = 5;
    public const int MinStickyDays = 0;
    public const int MaxStickyDays = 365;
    public const int DefaultStickyDays = 7;
    public const string StickyCookiePrefix = ".yes.exp.";
    public const string ExperimentKeySuffix = "-ab";
    public const string RouteMetadataKey = "Yes.Experiment";
    public const string ControlVariantKey = "A";
    public const int MaxWeightPercent = 100;
    public const int VariantKeyMaxLength = 20;
    public const string VariantKeyPattern = "^[A-Z]$";
    public const string CookieNamePattern = "^[A-Za-z0-9!#$%&'*+.^_`|~-]{1,100}$";
    public const int RuleNameMaxLength = 100;
    public const int RuleValueMaxLength = 255;

    // 自動回退（AutoRollbackRule）
    public const string MetricFiveXxRate = "5xxRate";
    public const decimal DefaultThresholdPercent = 2;
    public const int DefaultDurationMin = 5;
    public const int DefaultMinSamples = 100;
    public const int MinDurationMin = 1;
    public const int MaxDurationMin = 60;

    // 閘道計數與監測
    public const int MetricsBucketSeconds = 10;
    public const int MetricsBucketCount = 60;
    public const int RollbackEvaluateIntervalSeconds = 10;
    public const int ScheduleAuditIntervalSeconds = 60;
    public const int SecondsPerDay = 86400;
    public const int ServerErrorMin = 500;
    public const int ServerErrorMax = 599;
}
