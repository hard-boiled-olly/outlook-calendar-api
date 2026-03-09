namespace OutlookCalendarApi.Models.Domain;

public class SchedulingPreferences
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public TimeOnly WorkingHoursStart { get; set; } = new(9, 0);
    public TimeOnly WorkingHoursEnd { get; set; } = new(17, 30);
    public Dictionary<string, string> PreferredTimes { get; set; } = new();
    public required string TimeZone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
