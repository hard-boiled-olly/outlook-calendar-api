namespace OutlookCalendarApi.Models.Domain;

public class Identity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Statement { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Summit> Summits { get; set; } = [];
    public ICollection<Habit> Habits { get; set; } = [];
}
