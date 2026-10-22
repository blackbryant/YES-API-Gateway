using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>入口路由；導向與策略為空值時沿用服務設定（納入設定版本：是）</summary>
[Table("YarpRoutes")]
[Comment("入口路由表：設定外部請求如何進來")]
public class YarpRoute : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Comment("所屬服務（YarpServices.Id）")]
    public long ServiceId { get; set; }

    [MaxLength(100)]
    [Comment("路由名稱，即 YARP 的 RouteId")]
    public string RouteKey { get; set; } = string.Empty;

    [Comment("流量導向的群組（空值沿用服務）")]
    public long? ClusterId { get; set; }

    [Comment("流量導向的 A/B 實驗，與 ClusterId 二擇一（空值沿用服務）")]
    public long? ExperimentId { get; set; }

    [MaxLength(255)]
    [Comment("網址配對規則")]
    public string MatchPath { get; set; } = string.Empty;

    [Comment("允許進來的 Domain")]
    public string[] MatchHosts { get; set; } = [];

    [Comment("允許的 HTTP 方法（空陣列代表全部）")]
    public string[] MatchMethods { get; set; } = [];

    [Comment("優先級（數字越小越優先）")]
    public int Order { get; set; }

    [Comment("覆寫限速策略（空值沿用服務）")]
    public long? RateLimitPolicyId { get; set; }

    [Comment("覆寫逾時策略（空值沿用服務）")]
    public long? TimeoutPolicyId { get; set; }

    [Comment("直接指定逾時秒數，優先於逾時策略")]
    public int? TimeoutSecOverride { get; set; }

    [Comment("覆寫授權策略（空值沿用服務）")]
    public long? AuthPolicyId { get; set; }

    [Comment("覆寫 CORS 策略（空值沿用服務）")]
    public long? CorsPolicyId { get; set; }

    [Comment("覆寫快取策略（空值沿用服務）")]
    public long? OutputCachePolicyId { get; set; }

    [Comment("請求 Body 上限位元組（空值沿用全域設定）")]
    public long? MaxRequestBodySize { get; set; }

    [Comment("是否啟用")]
    public bool IsActive { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
