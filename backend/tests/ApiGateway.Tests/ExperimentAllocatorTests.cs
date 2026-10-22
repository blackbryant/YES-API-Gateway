using ApiGateway.Gateway;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Models.Constants;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.Tests;

public class ExperimentAllocatorTests
{
    private const string CookieName = ".yes.exp.orders-api";
    private const int Samples = 10_000;

    private static ExperimentRuntimeDefinition Experiment(int weightA = 90, int weightB = 10, int stickyDays = 7, params ForceRuleDefinition[] rules) =>
        new(new ExperimentDefinition
        {
            Key = "orders-api-ab",
            Status = ExperimentConfigStatus.Running,
            Sticky = new StickyDefinition { CookieName = CookieName, Days = stickyDays },
            Variants =
            [
                new VariantDefinition { Key = "A", ClusterId = "CA", Weight = weightA, IsControl = true },
                new VariantDefinition { Key = "B", ClusterId = "CB", Weight = weightB },
            ],
            ForceRules = [.. rules],
        });

    private static HttpRequest Request(string? cookie = null, string? header = null, string? query = null)
    {
        var context = new DefaultHttpContext();
        if (cookie is not null)
        {
            context.Request.Headers.Cookie = $"{CookieName}={cookie}";
        }

        if (header is not null)
        {
            context.Request.Headers["X-Exp-Group"] = header;
        }

        if (query is not null)
        {
            context.Request.QueryString = new QueryString($"?exp={query}");
        }

        return context.Request;
    }

    private static readonly ForceRuleDefinition HeaderRule = new() { Type = ExperimentRuleTypes.Header, Name = "X-Exp-Group" };

    [Fact]
    public void Weighted_9010_WithinThreePercentagePoints()
    {
        var random = new Random(20261022);
        var experiment = Experiment();

        var b = Enumerable.Range(0, Samples).Count(_ =>
            ExperimentAllocator.Allocate(Request(), experiment, ExperimentStatus.Running, random).Variant.Key == "B");

        Assert.InRange(b * 100.0 / Samples, 7, 13);
    }

    [Fact]
    public void Weighted_WritesStickyCookie()
    {
        var result = ExperimentAllocator.Allocate(Request(), Experiment(), ExperimentStatus.Running, new Random(1));

        Assert.True(result.WriteStickyCookie);
    }

    [Fact]
    public void StickyDaysZero_DoesNotWriteCookie()
    {
        var result = ExperimentAllocator.Allocate(Request(), Experiment(stickyDays: 0), ExperimentStatus.Running, new Random(1));

        Assert.False(result.WriteStickyCookie);
    }

    [Fact]
    public void StickyCookie_KeepsVariantWithoutRewriting()
    {
        var experiment = Experiment();

        for (var i = 0; i < 100; i++)
        {
            var result = ExperimentAllocator.Allocate(Request(cookie: "B"), experiment, ExperimentStatus.Running, new Random(i));
            Assert.Equal("B", result.Variant.Key);
            Assert.False(result.WriteStickyCookie);
        }
    }

    [Fact]
    public void ForceHeader_BeatsStickyCookie()
    {
        var result = ExperimentAllocator.Allocate(Request(cookie: "A", header: "b"), Experiment(rules: HeaderRule), ExperimentStatus.Running, new Random(1));

        Assert.Equal("B", result.Variant.Key);
        Assert.False(result.WriteStickyCookie);
    }

    [Fact]
    public void ForceHeader_UnknownVariant_IsIgnored()
    {
        var result = ExperimentAllocator.Allocate(Request(cookie: "B", header: "Z"), Experiment(rules: HeaderRule), ExperimentStatus.Running, new Random(1));

        Assert.Equal("B", result.Variant.Key);
    }

    [Fact]
    public void ForceRuleWithValue_MapsToVariant()
    {
        var rule = new ForceRuleDefinition { Type = ExperimentRuleTypes.Query, Name = "exp", Value = "beta", Variant = "B" };

        var result = ExperimentAllocator.Allocate(Request(query: "beta"), Experiment(100, 0, 7, rule), ExperimentStatus.Running, new Random(1));

        Assert.Equal("B", result.Variant.Key);
    }

    [Fact]
    public void StickyCookie_UnknownVariant_ReallocatesAndRewrites()
    {
        var result = ExperimentAllocator.Allocate(Request(cookie: "Z"), Experiment(), ExperimentStatus.Running, new Random(1));

        Assert.True(result.WriteStickyCookie);
        Assert.Contains(result.Variant.Key, new[] { "A", "B" });
    }

    [Fact]
    public void ZeroWeightVariant_IsNeverDrawn()
    {
        var experiment = Experiment(100, 0);
        var random = new Random(7);

        Assert.All(Enumerable.Range(0, 1000), _ =>
            Assert.Equal("A", ExperimentAllocator.Allocate(Request(), experiment, ExperimentStatus.Running, random).Variant.Key));
    }

    [Theory]
    [InlineData(ExperimentStatus.Draft)]
    [InlineData(ExperimentStatus.Scheduled)]
    [InlineData(ExperimentStatus.Paused)]
    [InlineData(ExperimentStatus.Ended)]
    public void NotRunning_AlwaysControl_IgnoresForceAndSticky(ExperimentStatus status)
    {
        var result = ExperimentAllocator.Allocate(Request(cookie: "B", header: "B"), Experiment(rules: HeaderRule), status, new Random(1));

        Assert.Equal("A", result.Variant.Key);
        Assert.False(result.WriteStickyCookie);
    }
}
