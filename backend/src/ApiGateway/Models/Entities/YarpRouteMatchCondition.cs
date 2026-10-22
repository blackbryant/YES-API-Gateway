using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>路由額外比對條件（納入設定版本：是）</summary>
[Table("YarpRouteMatchConditions")]
[Comment("路由比對條件表：依標頭或 Query 參數比對")]
public class YarpRouteMatchCondition : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Comment("所屬路由（YarpRoutes.Id）")]
    public long RouteId { get; set; }

    [MaxLength(10)]
    [Comment("條件類型（Header / Query）")]
    public string ConditionType { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("標頭或參數名稱")]
    public string Name { get; set; } = string.Empty;

    [Comment("比對值")]
    public string[] Values { get; set; } = [];

    [MaxLength(30)]
    [Comment("比對方式")]
    public string Mode { get; set; } = string.Empty;

    [Comment("是否區分大小寫")]
    public bool IsCaseSensitive { get; set; }

    [Comment("顯示順序")]
    public int SortOrder { get; set; }
}
