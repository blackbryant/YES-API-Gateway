using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>實驗變體（納入設定版本：是）</summary>
[Table("YarpExperimentVariants")]
[Comment("實驗變體表：各組別指向一個群組並設定流量比例")]
public class YarpExperimentVariant : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Comment("所屬實驗（YarpExperiments.Id）")]
    public long ExperimentId { get; set; }

    [MaxLength(20)]
    [Comment("組別代號，與 ExperimentId 組合唯一")]
    public string VariantKey { get; set; } = string.Empty;

    [Comment("導向的群組（YarpClusters.Id）")]
    public long ClusterId { get; set; }

    [Comment("流量比例 0–100（同一實驗加總須為 100）")]
    public short WeightPercent { get; set; }

    [Comment("是否為對照組")]
    public bool IsControl { get; set; }
}
