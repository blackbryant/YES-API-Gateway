using System.Collections.Concurrent;
using System.Collections.Frozen;
using ApiGateway.Models.Constants;
using ApiGateway.Models.Entities;

namespace ApiGateway.Gateway.Experiments;

/// <summary>實驗的執行期覆寫（手動暫停 / 結束、自動回退）</summary>
public sealed record ExperimentRuntimeOverride(string? OverrideStatus, bool IsAutoRolledBack, DateTimeOffset? TriggeredAt);

/// <summary>
/// 執行期覆寫的記憶體快取（Singleton）。資料庫內容由 ConfigSyncService 每輪替換；
/// 本機自動回退覆寫在資料庫尚未反映前優先生效，確保資料庫異常時仍能切回對照組。
/// </summary>
public sealed class ExperimentRuntimeCache
{
    private readonly ConcurrentDictionary<string, ExperimentRuntimeOverride> _local = new(StringComparer.Ordinal);
    private volatile FrozenDictionary<string, ExperimentRuntimeOverride> _stored = FrozenDictionary<string, ExperimentRuntimeOverride>.Empty;

    public ExperimentRuntimeOverride? Get(string experimentKey) =>
        _local.TryGetValue(experimentKey, out var local) ? local
        : _stored.TryGetValue(experimentKey, out var stored) ? stored
        : null;

    /// <summary>以資料庫內容替換；資料庫已反映自動回退時移除本機覆寫</summary>
    public void ReplaceFromStorage(IEnumerable<YarpExperimentRuntime> rows)
    {
        _stored = rows
            .Where(r => r.OverrideStatus is not null)
            .ToFrozenDictionary(r => r.ExperimentKey, r => new ExperimentRuntimeOverride(r.OverrideStatus, r.IsAutoRolledBack, r.TriggeredAt), StringComparer.Ordinal);

        foreach (var key in _local.Keys)
        {
            if (_stored.ContainsKey(key))
            {
                _local.TryRemove(key, out _);
            }
        }
    }

    /// <summary>本機觸發自動回退：立即生效</summary>
    public void SetLocalRollback(string experimentKey, DateTimeOffset triggeredAt) =>
        _local[experimentKey] = new ExperimentRuntimeOverride(ExperimentOverrideStatus.Paused, true, triggeredAt);

    /// <summary>手動操作後同步本機狀態（不等待下一輪輪詢）</summary>
    public void SetStored(string experimentKey, ExperimentRuntimeOverride? value)
    {
        _local.TryRemove(experimentKey, out _);
        var copy = _stored.ToDictionary(StringComparer.Ordinal);
        if (value?.OverrideStatus is null)
        {
            copy.Remove(experimentKey);
        }
        else
        {
            copy[experimentKey] = value;
        }

        _stored = copy.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public bool HasLocalOverride(string experimentKey) => _local.ContainsKey(experimentKey);
}
