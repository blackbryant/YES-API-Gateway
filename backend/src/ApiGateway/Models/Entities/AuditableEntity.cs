using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>設定表共用欄位（yarp_schema_v2：CreatedAt、UpdatedAt、UpdatedBy）。時間由 GatewayDbContext 於 SaveChanges 時自動寫入。</summary>
public abstract class AuditableEntity
{
    [Comment("建立時間")]
    public DateTimeOffset CreatedAt { get; set; }

    [Comment("更新時間")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Comment("最後異動者（AppUsers.Id）")]
    public long? UpdatedBy { get; set; }
}
