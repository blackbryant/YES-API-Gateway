using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>A/B 實驗（納入設定版本：是）</summary>
[Table("YarpExperiments")]
[Comment("A/B 實驗表：同一路由依比例、標頭或 Cookie 分流")]
public class YarpExperiment : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Comment("實驗名稱")]
    public string ExperimentKey { get; set; } = string.Empty;

    [Comment("所屬服務（YarpServices.Id）")]
    public long ServiceId { get; set; }

    [MaxLength(20)]
    [Comment("設定狀態（Draft / Running），有效狀態由執行期計算")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("黏著 Cookie 名稱")]
    public string StickyCookieName { get; set; } = string.Empty;

    [Comment("黏著天數")]
    public int StickyDays { get; set; }

    [Comment("開始時間")]
    public DateTimeOffset? StartAt { get; set; }

    [Comment("自動結束時間")]
    public DateTimeOffset? EndAt { get; set; }

    [Column(TypeName = "jsonb")]
    [Comment("自動回退條件")]
    public string? AutoRollbackRule { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
