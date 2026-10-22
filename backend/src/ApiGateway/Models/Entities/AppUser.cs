using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>管理介面帳號（納入設定版本：否）</summary>
[Table("AppUsers")]
[Comment("帳號表：admin（可編輯、發佈）與 user（唯讀）")]
public class AppUser
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(50)]
    [Comment("登入帳號")]
    public string Username { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("顯示名稱")]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("密碼雜湊（ASP.NET Core PasswordHasher）")]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(10)]
    [Comment("角色（admin / user）")]
    public string Role { get; set; } = string.Empty;

    [Comment("是否啟用")]
    public bool IsActive { get; set; }

    [Comment("下次登入強制變更密碼")]
    public bool MustChangePassword { get; set; }

    [Comment("連續登入失敗次數")]
    public int FailedLoginCount { get; set; }

    [Comment("鎖定解除時間")]
    public DateTimeOffset? LockoutEndAt { get; set; }

    [Comment("最後登入時間")]
    public DateTimeOffset? LastLoginAt { get; set; }

    [Comment("建立時間")]
    public DateTimeOffset CreatedAt { get; set; }

    [Comment("更新時間")]
    public DateTimeOffset UpdatedAt { get; set; }
}
