using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>稽核日誌（納入設定版本：否）</summary>
[Table("AuditLogs")]
[Comment("稽核日誌表：設定異動、發佈、回滾與登入事件")]
public class AuditLog
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [Comment("發生時間")]
    public DateTimeOffset OccurredAt { get; set; }

    [Comment("操作者（AppUsers.Id；系統動作為空值）")]
    public long? UserId { get; set; }

    [MaxLength(30)]
    [Comment("動作（Create, Update, Delete, Publish, Rollback, Login, LoginFailed）")]
    public string Action { get; set; } = string.Empty;

    [MaxLength(50)]
    [Comment("資料表")]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("資料名稱")]
    public string? EntityKey { get; set; }

    [Column(TypeName = "jsonb")]
    [Comment("異動前內容")]
    public string? Before { get; set; }

    [Column(TypeName = "jsonb")]
    [Comment("異動後內容")]
    public string? After { get; set; }

    [Comment("來源 IP")]
    public IPAddress? ClientIp { get; set; }
}
