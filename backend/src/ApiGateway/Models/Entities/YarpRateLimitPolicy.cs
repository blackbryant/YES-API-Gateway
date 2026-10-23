using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>限速策略（納入設定版本：是）</summary>
[Table("YarpRateLimitPolicies")]
[Comment("限速策略表：可被多條路由共用的限速規則")]
public class YarpRateLimitPolicy : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Comment("限速策略名稱")]
    public string PolicyName { get; set; } = string.Empty;

    [MaxLength(30)]
    [Comment("限流演算法（FixedWindow, SlidingWindow, TokenBucket, Concurrency）")]
    public string LimitType { get; set; } = string.Empty;

    [Comment("時間區間內最大請求數")]
    public int? PermitLimit { get; set; }

    [Comment("時間區間秒數")]
    public int? WindowSec { get; set; }

    [Comment("滑動視窗分段數")]
    public int? SegmentsPerWindow { get; set; }

    [Comment("令牌桶容量")]
    public int? TokenLimit { get; set; }

    [Comment("每次補充的令牌數")]
    public int? TokensPerPeriod { get; set; }

    [Comment("令牌補充間隔秒數")]
    public int? ReplenishmentPeriodSec { get; set; }

    [Comment("排隊上限（0 代表不排隊）")]
    public int QueueLimit { get; set; }

    [MaxLength(20)]
    [Comment("排隊處理順序（OldestFirst / NewestFirst）")]
    public string QueueProcessingOrder { get; set; } = string.Empty;

    [MaxLength(20)]
    [Comment("計數依據（ClientIp, User, ApiKey, Header, Global）")]
    public string PartitionBy { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("依 ApiKey / Header 時的標頭名稱，依 User 時的 Claim")]
    public string? PartitionKeyName { get; set; }

    [Comment("超過上限的回應碼")]
    public int RejectionStatusCode { get; set; }

    [Comment("是否為系統內建（不可刪除）")]
    public bool IsBuiltIn { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
