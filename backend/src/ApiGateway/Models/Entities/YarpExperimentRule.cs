using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>實驗分配規則（納入設定版本：是）</summary>
[Table("YarpExperimentRules")]
[Comment("實驗分配規則表：依序比對，先符合者生效")]
public class YarpExperimentRule : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Comment("所屬實驗（YarpExperiments.Id）")]
    public long ExperimentId { get; set; }

    [Comment("比對順序")]
    public int SortOrder { get; set; }

    [MaxLength(20)]
    [Comment("條件類型（Header, Cookie, Query, Default）")]
    public string MatchType { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("標頭、Cookie 或參數名稱")]
    public string? MatchName { get; set; }

    [MaxLength(255)]
    [Comment("比對值（空值代表存在即符合）")]
    public string? MatchValue { get; set; }

    [MaxLength(20)]
    [Comment("符合時的動作（ForceVariant, Sticky, Weighted）")]
    public string Action { get; set; } = string.Empty;

    [Comment("強制指定的組別（YarpExperimentVariants.Id）")]
    public long? VariantId { get; set; }
}
