namespace OutlookCalendarApi.Models.Domain;

public class Sprint
{
    public Guid Id { get; set; }
    public Guid MilestoneId { get; set; }
    public int SprintNumber { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Reflection { get; set; }
    public required string Status { get; set; } // "active" | "completed" | "replanned"

    public Milestone Milestone { get; set; } = null!;
    public ICollection<HabitPrescription> HabitPrescriptions { get; set; } = [];
    public ICollection<HabitEvent> HabitEvents { get; set; } = [];
    public ICollection<SprintTask> SprintTasks { get; set; } = [];
}
