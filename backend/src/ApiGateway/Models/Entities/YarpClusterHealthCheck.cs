using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>群組健康檢查，與群組 1:1（納入設定版本：是）</summary>
[Table("YarpClusterHealthChecks")]
[Comment("群組健康檢查表：主動與被動健康檢查規則")]
public class YarpClusterHealthCheck : AuditableEntity
{
    [Comment("對應 YarpClusters.Id")]
    public long ClusterId { get; set; }

    [Comment("是否啟用主動健康檢查")]
    public bool ActiveEnabled { get; set; }

    [MaxLength(255)]
    [Comment("檢查路徑")]
    public string ActivePath { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("附加 Query 字串")]
    public string? ActiveQuery { get; set; }

    [Comment("檢查間隔秒數")]
    public int ActiveIntervalSec { get; set; }

    [Comment("單次檢查逾時秒數")]
    public int ActiveTimeoutSec { get; set; }

    [MaxLength(50)]
    [Comment("判定策略")]
    public string ActivePolicy { get; set; } = string.Empty;

    [Comment("連續失敗幾次判定不健康")]
    public int FailureThreshold { get; set; }

    [Comment("是否啟用被動健康檢查")]
    public bool PassiveEnabled { get; set; }

    [MaxLength(50)]
    [Comment("被動檢查策略")]
    public string PassivePolicy { get; set; } = string.Empty;

    [Comment("被隔離後多久恢復（秒）")]
    public int ReactivationPeriodSec { get; set; }

    [Precision(4, 3)]
    [Comment("失敗率門檻")]
    public decimal FailureRateLimit { get; set; }
}
