using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>請求與回應轉換（納入設定版本：是）</summary>
[Table("YarpRouteTransforms")]
[Comment("請求與回應轉換表：轉送前後改寫路徑、標頭，依序套用")]
public class YarpRouteTransform : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Comment("所屬路由（YarpRoutes.Id）")]
    public long RouteId { get; set; }

    [Comment("執行順序")]
    public int SortOrder { get; set; }

    [MaxLength(10)]
    [Comment("轉換方向（Request / Response）")]
    public string Direction { get; set; } = string.Empty;

    [MaxLength(50)]
    [Comment("轉換種類")]
    public string TransformType { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    [Comment("YARP 原生格式定義")]
    public string Definition { get; set; } = "{}";
}
