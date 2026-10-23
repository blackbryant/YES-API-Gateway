using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>CORS 策略（納入設定版本：是）</summary>
[Table("YarpCorsPolicies")]
[Comment("跨來源資源共用策略表")]
public class YarpCorsPolicy : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Comment("CORS 策略名稱（內建 default、disable）")]
    public string PolicyName { get; set; } = string.Empty;

    [Comment("允許的來源")]
    public string[] AllowedOrigins { get; set; } = [];

    [Comment("允許的方法（* 代表全部）")]
    public string[] AllowedMethods { get; set; } = [];

    [Comment("允許的標頭（* 代表全部）")]
    public string[] AllowedHeaders { get; set; } = [];

    [Comment("允許前端讀取的回應標頭")]
    public string[] ExposedHeaders { get; set; } = [];

    [Comment("是否允許帶 Cookie（為 true 時來源不可為 *）")]
    public bool AllowCredentials { get; set; }

    [Comment("預檢結果快取秒數")]
    public int PreflightMaxAgeSec { get; set; }

    [Comment("是否為系統內建（不可刪除）")]
    public bool IsBuiltIn { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
