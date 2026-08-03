using System.ComponentModel.DataAnnotations;

namespace NestFlow_Backend.Models.ParamModels;

public class SaveAccountEntryParamModel
{
    [Required(ErrorMessage = "請指定資料空間。")]
    public Guid WorkspaceId { get; set; }

    /// <summary>只接受 expense 或 income。</summary>
    [Required(ErrorMessage = "請指定類型。")]
    public string Type { get; set; } = string.Empty;

    [Range(0.01, 999999999999.99, ErrorMessage = "金額必須大於 0。")]
    public decimal Amount { get; set; }

    public string? Currency { get; set; }

    [Required(ErrorMessage = "請選擇分類。")]
    public string Category { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "備註不可超過 200 個字元。")]
    public string? Note { get; set; }

    /// <summary>發生時間，由前端依使用者時區換算為帶時區的時間字串後傳入。</summary>
    [Required(ErrorMessage = "請指定發生時間。")]
    public DateTimeOffset OccurredAt { get; set; }
}

public class UpdateTimeZoneParamModel
{
    [Required(ErrorMessage = "請指定時區。")]
    [StringLength(64, MinimumLength = 1)]
    public string TimeZone { get; set; } = string.Empty;
}
