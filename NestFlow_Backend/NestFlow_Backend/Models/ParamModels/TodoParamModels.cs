using System.ComponentModel.DataAnnotations;

namespace NestFlow_Backend.Models.ParamModels;

public class SaveTodoParamModel
{
    [Required(ErrorMessage = "請指定資料空間。")]
    public Guid WorkspaceId { get; set; }

    /// <summary>只接受 general 或 shopping。</summary>
    [Required(ErrorMessage = "請指定類型。")]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入代辦內容。")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "代辦內容不可超過 100 個字元。")]
    public string Title { get; set; } = string.Empty;

    /// <summary>購物清單的數量。一般代辦會忽略此欄位。</summary>
    [Range(1, 9999, ErrorMessage = "數量必須介於 1 至 9999。")]
    public int? Quantity { get; set; }

    /// <summary>到期時間，由前端依使用者時區換算後傳入。未設定時留空。</summary>
    public DateTimeOffset? DueAt { get; set; }
}

public class UpdateTodoCompletionParamModel
{
    public bool Completed { get; set; }
}
