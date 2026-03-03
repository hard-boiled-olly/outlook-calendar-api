namespace OutlookCalendarApi.Models.Domain;

public class Habit
{
    public Guid Id { get; set; }
    public Guid IdentityId { get; set; }
    public required string Name { get; set; }
    public required string Frequency { get; set; }
    public DateTime CreatedAt { get; set; }

    public Identity Identity { get; set; } = null!;
    public ICollection<HabitPrescription> HabitPrescriptions { get; set; } = [];
}
