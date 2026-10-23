using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>TLS 憑證（納入設定版本：是）</summary>
[Table("YarpCertificates")]
[Comment("TLS 憑證表：依 SNI 網域選擇憑證，私鑰不存明文")]
public class YarpCertificate : AuditableEntity
{
    [Comment("內部流水號")]
    public long Id { get; set; }

    [MaxLength(255)]
    [Comment("SNI 網域（支援萬用字元）")]
    public string Domain { get; set; } = string.Empty;

    [MaxLength(255)]
    [Comment("發行者")]
    public string Issuer { get; set; } = string.Empty;

    [Column(TypeName = "char(40)")]
    [Comment("憑證指紋")]
    public string Thumbprint { get; set; } = string.Empty;

    [Comment("生效日")]
    public DateTimeOffset NotBefore { get; set; }

    [Comment("到期日")]
    public DateTimeOffset NotAfter { get; set; }

    [MaxLength(20)]
    [Comment("存放方式（KeyVault, CertStore, EncryptedBlob）")]
    public string StorageType { get; set; } = string.Empty;

    [MaxLength(500)]
    [Comment("Key Vault URI 或憑證存放區路徑")]
    public string StorageRef { get; set; } = string.Empty;

    [Comment("加密後的憑證檔（僅 EncryptedBlob 使用）")]
    public byte[]? EncryptedPfx { get; set; }

    [Comment("綁定的端點")]
    public string[] BindEndpoints { get; set; } = [];

    [Comment("是否自動續期")]
    public bool AutoRenew { get; set; }

    [Comment("是否啟用")]
    public bool IsActive { get; set; }
}
