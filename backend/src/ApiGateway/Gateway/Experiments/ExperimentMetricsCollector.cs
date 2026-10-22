using System.Collections.Concurrent;
using ApiGateway.Models.Constants;

namespace ApiGateway.Gateway.Experiments;

/// <summary>某組別在統計視窗內的請求數與 5xx 數</summary>
public readonly record struct VariantWindowStats(long Total, long Errors);

/// <summary>
/// 各實驗各組別的回應計數（research R7）：每格 10 秒、共 60 格的環狀緩衝，
/// 以 Interlocked 累加，熱路徑無鎖、無 I/O（憲章七）。統計範圍為本節點。
/// </summary>
public sealed class ExperimentMetricsCollector
{
    private readonly ConcurrentDictionary<(string Experiment, string Variant), Window> _windows = new();
    private readonly TimeProvider _timeProvider;

    public ExperimentMetricsCollector(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void Record(string experimentKey, string variantKey, bool isServerError)
    {
        var window = _windows.GetOrAdd((experimentKey, variantKey), _ => new Window());
        window.Record(CurrentBucket(), isServerError);
    }

    /// <summary>最近 duration 內（含目前這一格）的計數</summary>
    public VariantWindowStats Snapshot(string experimentKey, string variantKey, TimeSpan duration)
    {
        if (!_windows.TryGetValue((experimentKey, variantKey), out var window))
        {
            return default;
        }

        var buckets = (int)Math.Clamp(Math.Ceiling(duration.TotalSeconds / ExperimentRules.MetricsBucketSeconds), 1, ExperimentRules.MetricsBucketCount);
        return window.Sum(CurrentBucket(), buckets);
    }

    /// <summary>清除實驗計數（手動恢復後重新監測）</summary>
    public void Reset(string experimentKey)
    {
        foreach (var key in _windows.Keys.Where(k => k.Experiment == experimentKey))
        {
            _windows.TryRemove(key, out _);
        }
    }

    public static bool IsServerError(int statusCode) =>
        statusCode is >= ExperimentRules.ServerErrorMin and <= ExperimentRules.ServerErrorMax;

    private long CurrentBucket() => _timeProvider.GetUtcNow().ToUnixTimeSeconds() / ExperimentRules.MetricsBucketSeconds;

    private sealed class Window
    {
        private readonly long[] _bucketIds = new long[ExperimentRules.MetricsBucketCount];
        private readonly long[] _totals = new long[ExperimentRules.MetricsBucketCount];
        private readonly long[] _errors = new long[ExperimentRules.MetricsBucketCount];

        public void Record(long bucket, bool isError)
        {
            var index = (int)(bucket % ExperimentRules.MetricsBucketCount);
            var current = Interlocked.Read(ref _bucketIds[index]);
            if (current != bucket && Interlocked.CompareExchange(ref _bucketIds[index], bucket, current) == current)
            {
                // 輪到新的一格：清空舊計數（同時寫入的少量誤差可接受）
                Interlocked.Exchange(ref _totals[index], 0);
                Interlocked.Exchange(ref _errors[index], 0);
            }

            Interlocked.Increment(ref _totals[index]);
            if (isError)
            {
                Interlocked.Increment(ref _errors[index]);
            }
        }

        public VariantWindowStats Sum(long currentBucket, int buckets)
        {
            long total = 0, errors = 0;
            for (var bucket = currentBucket - buckets + 1; bucket <= currentBucket; bucket++)
            {
                var index = (int)(bucket % ExperimentRules.MetricsBucketCount);
                if (Interlocked.Read(ref _bucketIds[index]) == bucket)
                {
                    total += Interlocked.Read(ref _totals[index]);
                    errors += Interlocked.Read(ref _errors[index]);
                }
            }

            return new VariantWindowStats(total, errors);
        }
    }
}
