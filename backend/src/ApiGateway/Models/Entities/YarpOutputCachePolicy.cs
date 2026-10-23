using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>輸出快取策略（納入設定版本：是）</summary>
[Table("YarpOutputCachePolicies")]
[Comment("輸出快取策略表：命中快取時不轉送到後端")]
public class YarpOutputCachePolicy : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Comment("快取策略名稱")]
    public string PolicyName { get; set; } = string.Empty;

    [Comment("快取存活秒數")]
    public int ExpireSec { get; set; }

    [Comment("依這些 Query 參數分開快取")]
    public string[] VaryByQuery { get; set; } = [];

    [Comment("依這些標頭分開快取")]
    public string[] VaryByHeader { get; set; } = [];

    [Comment("依這些路由參數分開快取")]
    public string[] VaryByRouteValue { get; set; } = [];

    [Comment("清除快取時使用的標籤")]
    public string[] Tags { get; set; } = [];

    [Comment("明確不快取")]
    public bool NoStore { get; set; }

    [Comment("回應超過此大小不快取")]
    public long? MaxBodySizeBytes { get; set; }

    [Comment("是否為系統內建（不可刪除）")]
    public bool IsBuiltIn { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
