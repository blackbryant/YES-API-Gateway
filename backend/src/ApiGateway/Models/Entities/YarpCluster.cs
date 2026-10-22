using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>主機群組（納入設定版本：是）</summary>
[Table("YarpClusters")]
[Comment("主機群組表：將相同功能的主機打包並定義負載平衡方式")]
public class YarpCluster : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Comment("系統產生 ULID，即 YARP 的 ClusterId")]
    public string ClusterKey { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("畫面顯示名稱")]
    public string DisplayName { get; set; } = string.Empty;

    [Comment("由服務自動建立時的所屬服務（YarpServices.Id）")]
    public long? OwnerServiceId { get; set; }

    [MaxLength(50)]
    [Comment("負載平衡策略")]
    public string LoadBalancingPolicy { get; set; } = string.Empty;

    [MaxLength(10)]
    [Comment("對後端的 HTTP 版本（1.1 / 2.0 / 3.0）")]
    public string HttpVersion { get; set; } = string.Empty;

    [MaxLength(30)]
    [Comment("版本協商方式")]
    public string HttpVersionPolicy { get; set; } = string.Empty;

    [Comment("後端無回應即中斷的秒數")]
    public int ActivityTimeoutSec { get; set; }

    [Comment("每台後端最大連線數")]
    public int? MaxConnectionsPerServer { get; set; }

    [Comment("略過後端憑證驗證（僅限測試環境）")]
    public bool AcceptAnyServerCertificate { get; set; }

    [Column(TypeName = "jsonb")]
    [Comment("自訂屬性")]
    public string Metadata { get; set; } = "{}";

    [Comment("是否啟用")]
    public bool IsActive { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
