namespace NestFlow_Backend.Models.ViewModels;

public class StorageItemViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;
}
