namespace OutlookCalendarApi.Models.Domain;

public class InterviewSession
{
    public Guid Id { get; set; }
    public required string Type { get; set; } // "identity-summit" | "milestone-sprint"
    public Guid? IdentityId { get; set; }
    public Guid UserId { get; set; }
    public int CurrentStep { get; set; }
    public string ConversationHistory { get; set; } = "[]";
    public string AccumulatedData { get; set; } = "{}";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Identity? Identity { get; set; }
    public User User { get; set; } = null!;
}
