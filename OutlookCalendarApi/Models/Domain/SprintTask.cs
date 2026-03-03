namespace OutlookCalendarApi.Models.Domain;

public class SprintTask
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateOnly Deadline { get; set; }
    public int? DurationMins { get; set; }
    public string? CalendarEventId { get; set; }
    public required string Status { get; set; } // "pending" | "synced" | "completed"

    public Sprint Sprint { get; set; } = null!;
}
