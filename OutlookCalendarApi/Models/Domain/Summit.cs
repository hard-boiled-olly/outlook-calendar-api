namespace OutlookCalendarApi.Models.Domain;

public class Summit
{
    public Guid Id { get; set; }
    public Guid IdentityId { get; set; }
    public required string Description { get; set; }
    public required string ProofCriteria { get; set; }
    public DateOnly? TargetDate { get; set; }
    public required string Status { get; set; } // "active" | "achieved" | "abandoned"
    public DateTime CreatedAt { get; set; }

    public Identity Identity { get; set; } = null!;
    public ICollection<Milestone> Milestones { get; set; } = [];
}
