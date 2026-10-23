using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Models.Entities;

/// <summary>草稿狀態（單列，納入設定版本：否）</summary>
[Table("YarpConfigDraft")]
[Comment("草稿狀態表：固定一列，記錄草稿基礎版本與未發佈變更數")]
public class YarpConfigDraft
{
    [Comment("固定為 1")]
    public short Id { get; set; }

    [Column(TypeName = "char(10)")]
    [Comment("草稿的基礎版本")]
    public string? BaseVersionNo { get; set; }

    [Comment("是否有未發佈變更")]
    public bool IsDirty { get; set; }

    [Comment("未發佈變更筆數")]
    public int ChangeCount { get; set; }

    [Comment("最後編輯者（AppUsers.Id）")]
    public long? LastEditedBy { get; set; }

    [Comment("最後編輯時間")]
    public DateTimeOffset? LastEditedAt { get; set; }
}
