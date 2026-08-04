using NestFlow_Backend.Common;

namespace NestFlow_Backend.Models.Entities;

public class StorageItem
{
    public Guid Id { get; set; }

    /// <summary>建立這筆紀錄的使用者。家庭空間內任何成員都可修改，此欄位僅供顯示與追溯。</summary>
    public Guid UserId { get; set; }

    public Guid WorkspaceId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>存放位置。整個功能的用途就是回答「東西放在哪裡」，因此為必填。</summary>
    public string Location { get; set; } = string.Empty;

    public string? Note { get; set; }

    public StorageItemStatus Status { get; set; } = StorageItemStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>最後更新時間。最近更新列表依此排序。</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public User? User { get; set; }

    public Workspace? Workspace { get; set; }
}
