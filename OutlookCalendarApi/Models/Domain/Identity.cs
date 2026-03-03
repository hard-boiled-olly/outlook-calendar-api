namespace OutlookCalendarApi.Models.Domain;

public class Identity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string AreaOfLife { get; set; }
    public required string Statement { get; set; }
    public required string Status { get; set; } // "active" | "abandoned"
    public DateTime CreatedAt { get; set; }
    public DateTime? AbandonedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Summit> Summits { get; set; } = [];
    public ICollection<Habit> Habits { get; set; } = [];
}
