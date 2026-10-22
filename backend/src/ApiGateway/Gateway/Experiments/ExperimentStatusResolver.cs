using ApiGateway.Models.Constants;

namespace ApiGateway.Gateway.Experiments;

/// <summary>
/// 實驗有效狀態（research R6）：設定 + 執行期覆寫 + 時間。純函式，各節點以相同規則計算。
/// 排程由時間判斷，不需寫入資料庫，到點即生效。
/// </summary>
public static class ExperimentStatusResolver
{
    public static ExperimentStatus Resolve(ExperimentDefinition? definition, string? overrideStatus, DateTimeOffset now)
    {
        if (definition is null || definition.Status != ExperimentConfigStatus.Running)
        {
            return ExperimentStatus.Draft;
        }

        if (overrideStatus == ExperimentOverrideStatus.Ended || (definition.EndAt is { } end && now >= end))
        {
            return ExperimentStatus.Ended;
        }

        if (overrideStatus == ExperimentOverrideStatus.Paused)
        {
            return ExperimentStatus.Paused;
        }

        if (definition.StartAt is { } start && now < start)
        {
            return ExperimentStatus.Scheduled;
        }

        return ExperimentStatus.Running;
    }
}
