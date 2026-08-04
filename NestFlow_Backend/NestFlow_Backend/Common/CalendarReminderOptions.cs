namespace NestFlow_Backend.Common;

/// <summary>
/// 行程提醒的「提前多久通知」固定選單。與記帳分類清單同樣的設計：
/// 固定選項，前端顯示名稱、後端只認分鐘數並驗證白名單。
/// </summary>
public static class CalendarReminderOptions
{
    /// <summary>準時、5 分鐘前、10 分鐘前、30 分鐘前、1 小時前、1 天前。</summary>
    public static readonly IReadOnlyList<int> LeadMinutes = [0, 5, 10, 30, 60, 1440];

    public static int Normalize(int? minutesBeforeStart)
    {
        if (minutesBeforeStart is null || !LeadMinutes.Contains(minutesBeforeStart.Value))
        {
            throw AppException.BadRequest("提前通知時間必須是固定選項之一。");
        }

        return minutesBeforeStart.Value;
    }
}
