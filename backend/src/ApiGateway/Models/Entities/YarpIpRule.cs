using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>IP 黑白名單（Manual 納入設定版本；Auto 自動封鎖不納入）</summary>
[Table("YarpIpRules")]
[Comment("IP 黑白名單表：手動與 DDoS 自動封鎖的 IP 規則")]
public class YarpIpRule : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Column(TypeName = "cidr")]
    [Comment("IP 範圍")]
    public IPNetwork Cidr { get; set; }

    [MaxLength(10)]
    [Comment("規則類型（Block / Allow）")]
    public string RuleType { get; set; } = string.Empty;

    [MaxLength(10)]
    [Comment("來源（Manual / Auto）")]
    public string Source { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("原因")]
    public string? Reason { get; set; }

    [Comment("自動解除時間")]
    public DateTimeOffset? ExpiresAt { get; set; }
}
