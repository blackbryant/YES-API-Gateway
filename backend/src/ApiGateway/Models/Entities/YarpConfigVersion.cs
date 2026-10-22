using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>設定版本：發佈產生的不可變快照</summary>
[Table("YarpConfigVersions")]
[Comment("設定版本表：每次發佈產生一個版本，最多保留 20 版")]
public class YarpConfigVersion
{
    [Column(TypeName = "char(10)")]
    [Comment("版本號（YYYYMMDD + 兩碼流水號）")]
    public string VersionNo { get; set; } = string.Empty;

    [MaxLength(20)]
    [Comment("版本狀態（Active / History / RolledBack），Active 同時只有一筆")]
    public string Status { get; set; } = string.Empty;

    [Column(TypeName = "char(10)")]
    [Comment("發佈當下的生效版本")]
    public string? BasedOnVersionNo { get; set; }

    [Column(TypeName = "jsonb")]
    [Comment("所有設定表的完整內容（回滾時還原用）")]
    public string Snapshot { get; set; } = "{}";

    [Column(TypeName = "jsonb")]
    [Comment("由快照產生的 YARP ReverseProxy 設定（閘道直接載入）")]
    public string YarpConfig { get; set; } = "{}";

    [Column(TypeName = "char(64)")]
    [Comment("YarpConfig 的 SHA-256")]
    public string Checksum { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    [Comment("異動清單")]
    public string ChangeSummary { get; set; } = "[]";

    [Comment("異動筆數")]
    public int ChangeCount { get; set; }

    [MaxLength(500)]
    [Comment("發佈說明")]
    public string Note { get; set; } = string.Empty;

    [Comment("是否鎖定（鎖定後不會被自動淘汰）")]
    public bool IsLocked { get; set; }

    [Comment("發佈者（AppUsers.Id）")]
    public long PublishedBy { get; set; }

    [Comment("發佈時間")]
    public DateTimeOffset PublishedAt { get; set; }
}
