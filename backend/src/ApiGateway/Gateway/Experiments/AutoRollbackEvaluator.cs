namespace ApiGateway.Gateway.Experiments;

/// <summary>組別在視窗內的統計</summary>
public sealed record VariantStats(string VariantKey, bool IsControl, long Total, long Errors);

/// <summary>自動回退決策（觸發的組別與當時指標）</summary>
public sealed record RollbackDecision(string VariantKey, decimal RatePercent, long Samples, int WindowMin);

/// <summary>
/// 自動回退判斷（FR-019b）。純函式：只看非對照組；樣本數不足不觸發；
/// 5xx 比率超過門檻時回傳比率最高的組別。
/// </summary>
public static class AutoRollbackEvaluator
{
    private const decimal Percent = 100m;

    public static RollbackDecision? Evaluate(AutoRollbackDefinition rule, IEnumerable<VariantStats> stats) =>
        stats
            .Where(s => !s.IsControl && s.Total >= rule.MinSamples && s.Total > 0)
            .Select(s => new RollbackDecision(s.VariantKey, Math.Round(s.Errors * Percent / s.Total, 2), s.Total, rule.DurationMin))
            .Where(d => d.RatePercent > rule.ThresholdPercent)
            .OrderByDescending(d => d.RatePercent)
            .FirstOrDefault();
}
