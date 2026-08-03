using System.ComponentModel.DataAnnotations;

namespace NestFlow_Backend.Models.ParamModels;

public class SaveReminderParamModel
{
    [Required(ErrorMessage = "請指定資料空間。")]
    public Guid WorkspaceId { get; set; }

    [Required(ErrorMessage = "請輸入提醒內容。")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "提醒內容不可超過 200 個字元。")]
    public string Content { get; set; } = string.Empty;

    /// <summary>觸發時間，由前端依使用者時區換算為帶時區的時間字串後傳入。</summary>
    [Required(ErrorMessage = "請指定提醒時間。")]
    public DateTimeOffset TriggerAt { get; set; }
}
