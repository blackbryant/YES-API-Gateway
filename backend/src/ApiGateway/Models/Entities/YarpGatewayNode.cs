using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>閘道節點心跳與載入版本（納入設定版本：否）</summary>
[Table("YarpGatewayNodes")]
[Comment("閘道節點表：各節點心跳與目前載入的設定版本，用於判斷同步狀態")]
public class YarpGatewayNode
{
    [MaxLength(100)]
    [Comment("節點識別（Gateway:NodeId，未設定時為機器名稱）")]
    public string NodeId { get; set; } = string.Empty;

    [Column(TypeName = "char(10)")]
    [Comment("目前載入的版本號")]
    public string? LoadedVersionNo { get; set; }

    [Column(TypeName = "char(64)")]
    [Comment("目前載入版本的校驗碼")]
    public string? LoadedChecksum { get; set; }

    [Comment("最後心跳時間")]
    public DateTimeOffset LastHeartbeatAt { get; set; }
}
