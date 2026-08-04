using System.ComponentModel.DataAnnotations;

namespace NestFlow_Backend.Models.ParamModels;

public class SaveStorageItemParamModel
{
    [Required(ErrorMessage = "請指定資料空間。")]
    public Guid WorkspaceId { get; set; }

    [Required(ErrorMessage = "請輸入物品名稱。")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "物品名稱不可超過 100 個字元。")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入存放位置。")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "存放位置不可超過 100 個字元。")]
    public string Location { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "備註不可超過 200 個字元。")]
    public string? Note { get; set; }
}
