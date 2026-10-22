using ApiGateway.Gateway;
using ApiGateway.Gateway.Experiments;

namespace ApiGateway.Tests;

public class AutoRollbackEvaluatorTests
{
    private static readonly AutoRollbackDefinition Rule = new() { ThresholdPercent = 2, DurationMin = 1, MinSamples = 100 };

    [Fact]
    public void AboveThreshold_Triggers()
    {
        var decision = AutoRollbackEvaluator.Evaluate(Rule, [new("A", true, 5000, 0), new("B", false, 812, 35)]);

        Assert.NotNull(decision);
        Assert.Equal("B", decision.VariantKey);
        Assert.Equal(4.31m, decision.RatePercent);
        Assert.Equal(812, decision.Samples);
    }

    [Fact]
    public void InsufficientSamples_DoesNotTrigger() =>
        Assert.Null(AutoRollbackEvaluator.Evaluate(Rule, [new("A", true, 100, 0), new("B", false, 20, 20)]));

    [Fact]
    public void ControlGroupErrors_AreIgnored() =>
        Assert.Null(AutoRollbackEvaluator.Evaluate(Rule, [new("A", true, 1000, 900), new("B", false, 1000, 0)]));

    [Fact]
    public void AtThreshold_DoesNotTrigger() =>
        Assert.Null(AutoRollbackEvaluator.Evaluate(Rule, [new("B", false, 1000, 20)]));

    [Fact]
    public void MultipleVariants_PicksHighestRate()
    {
        var decision = AutoRollbackEvaluator.Evaluate(Rule, [new("B", false, 1000, 50), new("C", false, 1000, 300)]);

        Assert.Equal("C", decision!.VariantKey);
    }
}
