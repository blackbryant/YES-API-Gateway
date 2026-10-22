using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>A/B 實驗執行期狀態：手動暫停 / 結束、自動回退（納入設定版本：否）</summary>
[Table("YarpExperimentRuntime")]
[Comment("實驗執行期狀態表：手動暫停、結束與自動回退的覆寫，不納入設定版本")]
public class YarpExperimentRuntime
{
    [MaxLength(100)]
    [Comment("實驗名稱（對應生效版本中的 ExperimentKey）")]
    public string ExperimentKey { get; set; } = string.Empty;

    [MaxLength(20)]
    [Comment("覆寫狀態（Paused / Ended；空值代表無覆寫）")]
    public string? OverrideStatus { get; set; }

    [Comment("是否由自動回退造成的暫停")]
    public bool IsAutoRolledBack { get; set; }

    [Column(TypeName = "jsonb")]
    [Comment("自動回退觸發時的指標")]
    public string? TriggeredMetric { get; set; }

    [Comment("自動回退觸發時間")]
    public DateTimeOffset? TriggeredAt { get; set; }

    [MaxLength(100)]
    [Comment("觸發自動回退的閘道節點")]
    public string? TriggeredByNode { get; set; }

    [MaxLength(20)]
    [Comment("已記錄的排程事件（Started / Ended），避免多節點重複寫稽核")]
    public string? LastScheduleEvent { get; set; }

    [Comment("手動操作者（AppUsers.Id），系統操作為空值")]
    public long? UpdatedBy { get; set; }

    [Comment("最後更新時間")]
    public DateTimeOffset UpdatedAt { get; set; }
}
