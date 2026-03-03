namespace OutlookCalendarApi.Models.Domain;

public class HabitEvent
{
    public Guid Id { get; set; }
    public Guid HabitPrescriptionId { get; set; }
    public Guid SprintId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public TimeOnly? ScheduledTime { get; set; }
    public int DurationMins { get; set; }
    public string? CalendarEventId { get; set; }
    public required string Status { get; set; } // "pending" | "synced" | "completed" | "skipped"

    public HabitPrescription HabitPrescription { get; set; } = null!;
    public Sprint Sprint { get; set; } = null!;
}
