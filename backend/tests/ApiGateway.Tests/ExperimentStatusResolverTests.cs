using ApiGateway.Gateway;
using ApiGateway.Gateway.Experiments;
using ApiGateway.Models.Constants;

namespace ApiGateway.Tests;

public class ExperimentStatusResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);

    private static ExperimentDefinition Definition(string status = ExperimentConfigStatus.Running, int? startOffsetMin = null, int? endOffsetMin = null) => new()
    {
        Key = "exp",
        Status = status,
        StartAt = startOffsetMin is { } s ? Now.AddMinutes(s) : null,
        EndAt = endOffsetMin is { } e ? Now.AddMinutes(e) : null,
    };

    [Fact]
    public void NotPublished_IsDraft() =>
        Assert.Equal(ExperimentStatus.Draft, ExperimentStatusResolver.Resolve(null, null, Now));

    [Fact]
    public void ConfigDraft_IsDraft() =>
        Assert.Equal(ExperimentStatus.Draft, ExperimentStatusResolver.Resolve(Definition(ExperimentConfigStatus.Draft), null, Now));

    [Fact]
    public void Running_NoSchedule_IsRunning() =>
        Assert.Equal(ExperimentStatus.Running, ExperimentStatusResolver.Resolve(Definition(), null, Now));

    [Fact]
    public void BeforeStart_IsScheduled() =>
        Assert.Equal(ExperimentStatus.Scheduled, ExperimentStatusResolver.Resolve(Definition(startOffsetMin: 2), null, Now));

    [Fact]
    public void AtStart_IsRunning() =>
        Assert.Equal(ExperimentStatus.Running, ExperimentStatusResolver.Resolve(Definition(startOffsetMin: 0), null, Now));

    [Fact]
    public void AtEnd_IsEnded() =>
        Assert.Equal(ExperimentStatus.Ended, ExperimentStatusResolver.Resolve(Definition(endOffsetMin: 0), null, Now));

    [Fact]
    public void OverridePaused_IsPaused() =>
        Assert.Equal(ExperimentStatus.Paused, ExperimentStatusResolver.Resolve(Definition(), ExperimentOverrideStatus.Paused, Now));

    [Fact]
    public void OverrideEnded_IsEnded() =>
        Assert.Equal(ExperimentStatus.Ended, ExperimentStatusResolver.Resolve(Definition(), ExperimentOverrideStatus.Ended, Now));

    [Fact]
    public void PausedButPastEnd_IsEnded() =>
        Assert.Equal(ExperimentStatus.Ended, ExperimentStatusResolver.Resolve(Definition(endOffsetMin: -1), ExperimentOverrideStatus.Paused, Now));

    [Fact]
    public void PausedBeforeStart_IsPaused() =>
        Assert.Equal(ExperimentStatus.Paused, ExperimentStatusResolver.Resolve(Definition(startOffsetMin: 5), ExperimentOverrideStatus.Paused, Now));
}
