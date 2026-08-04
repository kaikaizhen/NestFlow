namespace NestFlow_Backend.Models.ViewModels;

public class CalendarEventViewModel
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public bool HasReminder { get; set; }

    public int? ReminderMinutesBeforeStart { get; set; }

    public bool IsRecurring { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByDisplayName { get; set; } = string.Empty;
}
