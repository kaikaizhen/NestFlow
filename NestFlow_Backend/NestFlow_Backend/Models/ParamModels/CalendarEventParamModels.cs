using System.ComponentModel.DataAnnotations;

namespace NestFlow_Backend.Models.ParamModels;

public class SaveCalendarEventParamModel
{
    [Required(ErrorMessage = "請指定資料空間。")]
    public Guid WorkspaceId { get; set; }

    [Required(ErrorMessage = "請輸入行程標題。")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "標題不可超過 100 個字元。")]
    public string Title { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "說明不可超過 500 個字元。")]
    public string? Description { get; set; }

    /// <summary>開始時間，由前端依使用者時區換算為帶時區的時間字串後傳入。</summary>
    [Required(ErrorMessage = "請指定開始時間。")]
    public DateTimeOffset StartAt { get; set; }

    [Required(ErrorMessage = "請指定結束時間。")]
    public DateTimeOffset EndAt { get; set; }
}
