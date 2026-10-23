using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>全域設定，一列一個區段（納入設定版本：是）</summary>
[Table("YarpGatewaySettings")]
[Comment("全域設定表：以 JSONB 存放 TLS、DDoS、API 版本、OpenTelemetry 等設定")]
public class YarpGatewaySetting : AuditableEntity
{
    [MaxLength(50)]
    [Comment("設定區段（Tls, HeaderHardening, Ddos, ApiVersioning, OpenTelemetry）")]
    public string Section { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    [Comment("該區段的設定值")]
    public string Settings { get; set; } = "{}";
}
