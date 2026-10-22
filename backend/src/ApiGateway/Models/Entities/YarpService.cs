using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>服務：管理介面的主要單位，發佈時展開到所屬路由，本身不輸出到 YARP（納入設定版本：是）</summary>
[Table("YarpServices")]
[Comment("服務表：一個服務等於一支對外 API，提供所屬路由共用的預設導向與策略")]
public class YarpService : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Comment("服務代碼，產生 RouteId 時作為前綴")]
    public string ServiceKey { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("畫面顯示名稱")]
    public string DisplayName { get; set; } = string.Empty;

    [Comment("預設導向的群組（YarpClusters.Id），與 DefaultExperimentId 二擇一")]
    public long? DefaultClusterId { get; set; }

    [Comment("預設導向的 A/B 實驗（YarpExperiments.Id）")]
    public long? DefaultExperimentId { get; set; }

    [Comment("可進入的監聽端點（External、Internal）")]
    public string[] AllowedListeners { get; set; } = [];

    [Comment("預設限速策略")]
    public long? RateLimitPolicyId { get; set; }

    [Comment("預設逾時策略")]
    public long? TimeoutPolicyId { get; set; }

    [Comment("預設授權策略")]
    public long? AuthPolicyId { get; set; }

    [Comment("預設 CORS 策略")]
    public long? CorsPolicyId { get; set; }

    [Comment("預設快取策略")]
    public long? OutputCachePolicyId { get; set; }

    [Comment("分類標籤")]
    public string[] Tags { get; set; } = [];

    [Comment("是否啟用（停用時所屬路由都不輸出）")]
    public bool IsActive { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
