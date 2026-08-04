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

    /// <summary>是否需要提醒。</summary>
    public bool WantsReminder { get; set; }

    /// <summary>提前幾分鐘通知，只在 WantsReminder 為 true 時使用，只接受固定選單的值。</summary>
    public int? ReminderMinutesBeforeStart { get; set; }

    /// <summary>是否建立週期行程，只在新增時生效，修改行程時會被忽略。</summary>
    public bool Repeat { get; set; }

    /// <summary>count｜until｜forever，只在 Repeat 為 true 時使用。</summary>
    public string? RepeatEndType { get; set; }

    /// <summary>RepeatEndType 為 count 時的重複次數。</summary>
    public int? RepeatCount { get; set; }

    /// <summary>RepeatEndType 為 until 時的結束日期。</summary>
    public DateTimeOffset? RepeatUntil { get; set; }
}
