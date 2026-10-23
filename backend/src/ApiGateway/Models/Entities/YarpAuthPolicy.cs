using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>授權策略（納入設定版本：是）</summary>
[Table("YarpAuthPolicies")]
[Comment("授權策略表：未通過授權的請求不會到達後端")]
public class YarpAuthPolicy : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(100)]
    [Comment("授權策略名稱（內建 default、anonymous）")]
    public string PolicyName { get; set; } = string.Empty;

    [Comment("使用的驗證方案（YarpAuthSchemes.Id，空值代表匿名）")]
    public long? AuthSchemeId { get; set; }

    [Comment("是否需登入")]
    public bool RequireAuthenticatedUser { get; set; }

    [Comment("需具備的角色（任一即可）")]
    public string[] RequiredRoles { get; set; } = [];

    [Column(TypeName = "jsonb")]
    [Comment("需具備的 Claim")]
    public string RequiredClaims { get; set; } = "[]";

    [Column(TypeName = "jsonb")]
    [Comment("Claim 轉成標頭送後端")]
    public string ForwardClaims { get; set; } = "{}";

    [Comment("是否為系統內建（不可刪除）")]
    public bool IsBuiltIn { get; set; }

    [MaxLength(500)]
    [Comment("備註")]
    public string? Description { get; set; }
}
