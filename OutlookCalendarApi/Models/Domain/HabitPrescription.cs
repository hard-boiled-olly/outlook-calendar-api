namespace OutlookCalendarApi.Models.Domain;

public class HabitPrescription
{
    public Guid Id { get; set; }
    public Guid HabitId { get; set; }
    public Guid SprintId { get; set; }
    public required string Prescription { get; set; }
    public DateTime CreatedAt { get; set; }

    public Habit Habit { get; set; } = null!;
    public Sprint Sprint { get; set; } = null!;
    public ICollection<HabitEvent> HabitEvents { get; set; } = [];
}
