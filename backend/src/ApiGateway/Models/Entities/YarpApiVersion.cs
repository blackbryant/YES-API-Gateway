using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>API 版本對應（納入設定版本：是）</summary>
[Table("YarpApiVersions")]
[Comment("API 版本對應表：每一列發佈時於所屬服務下產生一條路由")]
public class YarpApiVersion : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Comment("所屬服務（YarpServices.Id）")]
    public long ServiceId { get; set; }

    [MaxLength(20)]
    [Comment("版本，與 ServiceId 組合唯一")]
    public string Version { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("對外路徑")]
    public string PublicPath { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("轉送後端的路徑前綴")]
    public string BackendPathPrefix { get; set; } = string.Empty;

    [Comment("目標群組（YarpClusters.Id）")]
    public long ClusterId { get; set; }

    [Comment("產生的路由（YarpRoutes.Id）")]
    public long? RouteId { get; set; }

    [MaxLength(20)]
    [Comment("版本狀態（Preview, Current, Deprecated, Disabled）")]
    public string Status { get; set; } = string.Empty;

    [Comment("淘汰日期（寫入 Sunset 回應標頭）")]
    public DateOnly? SunsetDate { get; set; }

    [Comment("未帶版本時導向此版本")]
    public bool IsDefault { get; set; }
}
