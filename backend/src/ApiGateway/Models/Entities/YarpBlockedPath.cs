using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>隱藏內部端點（納入設定版本：是）</summary>
[Table("YarpBlockedPaths")]
[Comment("隱藏內部端點表：符合規則的路徑在閘道直接回應")]
public class YarpBlockedPath : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(255)]
    [Comment("路徑樣式")]
    public string PathPattern { get; set; } = string.Empty;

    [Comment("回應碼（404 或 403）")]
    public int ResponseStatusCode { get; set; }

    [Column(TypeName = "cidr[]")]
    [Comment("例外來源")]
    public IPNetwork[] AllowedCidrs { get; set; } = [];

    [Comment("是否啟用")]
    public bool IsActive { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
