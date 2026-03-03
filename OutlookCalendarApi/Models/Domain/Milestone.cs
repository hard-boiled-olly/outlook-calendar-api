namespace OutlookCalendarApi.Models.Domain;

public class Milestone
{
    public Guid Id { get; set; }
    public Guid SummitId { get; set; }
    public required string Description { get; set; }
    public required string ProofCriteria { get; set; }
    public DateOnly TargetDate { get; set; }
    public int SortOrder { get; set; }
    public required string Status { get; set; } // "pending" | "active" | "proved" | "missed"
    public DateTime? ProvedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Summit Summit { get; set; } = null!;
    public ICollection<Sprint> Sprints { get; set; } = [];
}
