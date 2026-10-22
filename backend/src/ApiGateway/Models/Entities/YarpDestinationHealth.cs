using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>主機健康狀態（執行期資料，納入設定版本：否）</summary>
[Table("YarpDestinationHealth")]
[Comment("主機健康狀態表：各閘道節點回寫的即時健康狀態，不納入設定版本")]
public class YarpDestinationHealth
{
    [Comment("對應 YarpDestinations.Id")]
    public long DestinationId { get; set; }

    [MaxLength(100)]
    [Comment("回報的閘道節點")]
    public string GatewayNode { get; set; } = string.Empty;

    [MaxLength(20)]
    [Comment("健康狀態（Healthy / Unhealthy / Unknown）")]
    public string Status { get; set; } = string.Empty;

    [Comment("連續失敗次數")]
    public int ConsecutiveFailures { get; set; }

    [Comment("最近檢查時間")]
    public DateTimeOffset LastCheckedAt { get; set; }

    [MaxLength(500)]
    [Comment("最近一次失敗原因")]
    public string? LastError { get; set; }
}
