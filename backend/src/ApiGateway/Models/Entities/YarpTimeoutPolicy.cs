using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>要求逾時策略（納入設定版本：是）</summary>
[Table("YarpTimeoutPolicies")]
[Comment("要求逾時策略表")]
public class YarpTimeoutPolicy : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Comment("逾時策略名稱")]
    public string PolicyName { get; set; } = string.Empty;

    [Comment("逾時秒數")]
    public int TimeoutSec { get; set; }

    [Comment("逾時回應碼")]
    public int TimeoutStatusCode { get; set; }

    [Comment("是否為系統內建（不可刪除）")]
    public bool IsBuiltIn { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
