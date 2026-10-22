using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>實體後端主機（納入設定版本：是）</summary>
[Table("YarpDestinations")]
[Comment("實體後端主機表：登記允許被代理的後端主機")]
public class YarpDestination : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Comment("隸屬的群組（YarpClusters.Id）")]
    public long ClusterId { get; set; }

    [MaxLength(100)]
    [Comment("YARP 內部識別碼，與 ClusterId 組合唯一")]
    public string DestinationKey { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("協定 + IP + Port")]
    public string Address { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("健康檢查位址，空值時使用 Address")]
    public string? HealthAddress { get; set; }

    [MaxLength(255)]
    [Comment("轉送時覆寫的 Host 標頭")]
    public string? Host { get; set; }

    [Column(TypeName = "jsonb")]
    [Comment("自訂屬性")]
    public string Metadata { get; set; } = "{}";

    [Comment("是否啟用（停用後不輸出到 YARP 設定）")]
    public bool IsActive { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
