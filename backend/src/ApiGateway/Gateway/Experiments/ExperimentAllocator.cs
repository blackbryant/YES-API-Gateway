using ApiGateway.Models.Constants;

namespace ApiGateway.Gateway.Experiments;

/// <summary>分配結果：進入的組別、是否需寫入黏著 Cookie</summary>
public readonly record struct AllocationResult(VariantDefinition Variant, bool WriteStickyCookie);

/// <summary>
/// A/B 分配（FR-017、research R5）。純函式，依序比對：強制分組 → 黏著 Cookie → 依權重抽籤。
/// 非 Running 一律回到對照組且不寫 Cookie；帶入不存在的組別時略過，依下一順位分配（FR-020）。
/// </summary>
public static class ExperimentAllocator
{
    public static AllocationResult Allocate(HttpRequest request, ExperimentRuntimeDefinition experiment, ExperimentStatus status, Random random)
    {
        if (status != ExperimentStatus.Running)
        {
            return new AllocationResult(experiment.Control, false);
        }

        foreach (var rule in experiment.Definition.ForceRules)
        {
            var value = ReadValue(request, rule.Type, rule.Name);
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var variantKey = rule.Value is null
                ? value.Trim().ToUpperInvariant()
                : string.Equals(value, rule.Value, StringComparison.Ordinal) ? rule.Variant : null;
            if (variantKey is not null && experiment.Variants.TryGetValue(variantKey, out var forced))
            {
                return new AllocationResult(forced, false);
            }
        }

        var sticky = experiment.Definition.Sticky;
        var stickyEnabled = sticky.Days > 0 && sticky.CookieName.Length > 0;
        if (stickyEnabled
            && request.Cookies.TryGetValue(sticky.CookieName, out var cookie)
            && cookie is not null
            && experiment.Variants.TryGetValue(cookie, out var stuck)
            && stuck.Weight > 0)
        {
            return new AllocationResult(stuck, false);
        }

        if (experiment.CumulativeWeights.Length == 0)
        {
            return new AllocationResult(experiment.Control, false);
        }

        var draw = random.Next(ExperimentRules.MaxWeightPercent);
        var chosen = experiment.CumulativeWeights.First(w => draw < w.UpperBound).Variant;
        return new AllocationResult(chosen, stickyEnabled);
    }

    private static string? ReadValue(HttpRequest request, string type, string name) => type switch
    {
        ExperimentRuleTypes.Header => request.Headers[name].FirstOrDefault(),
        ExperimentRuleTypes.Cookie => request.Cookies[name],
        ExperimentRuleTypes.Query => request.Query[name].FirstOrDefault(),
        _ => null,
    };
}
