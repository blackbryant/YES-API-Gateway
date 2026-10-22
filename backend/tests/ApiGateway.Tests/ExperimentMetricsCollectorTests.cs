using ApiGateway.Gateway.Experiments;
using Microsoft.Extensions.Time.Testing;

namespace ApiGateway.Tests;

public class ExperimentMetricsCollectorTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 22, 1, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Record_CountsTotalsAndErrors()
    {
        var collector = new ExperimentMetricsCollector(_time);

        collector.Record("exp", "B", isServerError: false);
        collector.Record("exp", "B", isServerError: true);
        collector.Record("exp", "A", isServerError: true);

        Assert.Equal(new VariantWindowStats(2, 1), collector.Snapshot("exp", "B", TimeSpan.FromMinutes(1)));
        Assert.Equal(new VariantWindowStats(1, 1), collector.Snapshot("exp", "A", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void OldBuckets_FallOutOfWindow()
    {
        var collector = new ExperimentMetricsCollector(_time);
        collector.Record("exp", "B", isServerError: true);

        _time.Advance(TimeSpan.FromMinutes(2));
        collector.Record("exp", "B", isServerError: false);

        Assert.Equal(new VariantWindowStats(1, 0), collector.Snapshot("exp", "B", TimeSpan.FromMinutes(1)));
        Assert.Equal(new VariantWindowStats(2, 1), collector.Snapshot("exp", "B", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void WrappedRingBuffer_ClearsReusedBucket()
    {
        var collector = new ExperimentMetricsCollector(_time);
        collector.Record("exp", "B", isServerError: true);

        // 60 格 × 10 秒 = 10 分鐘後回到同一格
        _time.Advance(TimeSpan.FromMinutes(10));
        collector.Record("exp", "B", isServerError: false);

        Assert.Equal(new VariantWindowStats(1, 0), collector.Snapshot("exp", "B", TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task ConcurrentRecords_AreAllCounted()
    {
        var collector = new ExperimentMetricsCollector(_time);
        const int threads = 16;
        const int perThread = 10_000;

        await Task.WhenAll(Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                collector.Record("exp", "B", i % 10 == 0);
            }
        })));

        Assert.Equal(new VariantWindowStats(threads * perThread, threads * perThread / 10), collector.Snapshot("exp", "B", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Reset_ClearsExperimentOnly()
    {
        var collector = new ExperimentMetricsCollector(_time);
        collector.Record("exp", "B", isServerError: true);
        collector.Record("other", "B", isServerError: true);

        collector.Reset("exp");

        Assert.Equal(default, collector.Snapshot("exp", "B", TimeSpan.FromMinutes(1)));
        Assert.Equal(1, collector.Snapshot("other", "B", TimeSpan.FromMinutes(1)).Total);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(502, true)]
    [InlineData(504, true)]
    [InlineData(499, false)]
    [InlineData(200, false)]
    public void IsServerError(int status, bool expected) => Assert.Equal(expected, ExperimentMetricsCollector.IsServerError(status));
}
