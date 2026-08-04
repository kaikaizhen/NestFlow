namespace NestFlow_Backend.Models.Dtos;

public class StorageItemDtoModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;
}

/// <summary>儲藏庫物品寫入用的命令，欄位皆已通過驗證與正規化。</summary>
public record SaveStorageItemCommand(string Name, string Location, string? Note);
