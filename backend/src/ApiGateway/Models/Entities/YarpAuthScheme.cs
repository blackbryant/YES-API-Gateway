using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>驗證方案（納入設定版本：是）</summary>
[Table("YarpAuthSchemes")]
[Comment("驗證方案表：定義閘道如何驗證身分")]
public class YarpAuthScheme : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(50)]
    [Comment("方案名稱")]
    public string SchemeName { get; set; } = string.Empty;

    [MaxLength(20)]
    [Comment("方案類型（JwtBearer, Cookie, ApiKey）")]
    public string SchemeType { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("驗證伺服器")]
    public string? Authority { get; set; }

    [MaxLength(100)]
    [Comment("受眾")]
    public string? Audience { get; set; }

    [Comment("JWKS 金鑰重新取得間隔（小時）")]
    public int MetadataRefreshHours { get; set; }

    [Column(TypeName = "jsonb")]
    [Comment("其他參數")]
    public string Options { get; set; } = "{}";
}
