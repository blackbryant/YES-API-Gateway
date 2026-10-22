using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>會話黏性，與群組 1:1（納入設定版本：是）</summary>
[Table("YarpClusterSessionAffinity")]
[Comment("會話黏性表：同一使用者固定送往同一台後端")]
public class YarpClusterSessionAffinity : AuditableEntity
{
    [Comment("對應 YarpClusters.Id")]
    public long ClusterId { get; set; }

    [Comment("是否啟用")]
    public bool Enabled { get; set; }

    [MaxLength(30)]
    [Comment("黏性方式（HashCookie, Cookie, ArrCookie, CustomHeader）")]
    public string Policy { get; set; } = string.Empty;

    [MaxLength(30)]
    [Comment("原主機不可用時的處理（Redistribute, Return503Error）")]
    public string FailurePolicy { get; set; } = string.Empty;

    [MaxLength(100)]
    [Comment("Cookie 或標頭名稱")]
    public string AffinityKeyName { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("Cookie Path")]
    public string CookiePath { get; set; } = "/";

    [MaxLength(255)]
    [Comment("Cookie Domain（空值為請求網域）")]
    public string? CookieDomain { get; set; }

    [Comment("Cookie 有效秒數")]
    public int? CookieExpirationSec { get; set; }

    [Comment("Cookie Max-Age")]
    public int? CookieMaxAgeSec { get; set; }

    [Comment("是否 HttpOnly")]
    public bool CookieHttpOnly { get; set; }

    [Comment("是否為必要 Cookie")]
    public bool CookieIsEssential { get; set; }

    [MaxLength(10)]
    [Comment("SameSite 設定（Lax, Strict, None）")]
    public string CookieSameSite { get; set; } = string.Empty;

    [MaxLength(20)]
    [Comment("Secure 設定（Always, SameAsRequest, None）")]
    public string CookieSecurePolicy { get; set; } = string.Empty;
}
