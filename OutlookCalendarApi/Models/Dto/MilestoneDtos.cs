namespace OutlookCalendarApi.Models.Dto;

public record MilestoneGenerationResult(List<GeneratedMilestoneItem> Milestones);

public record GeneratedMilestoneItem(
    string Description,
    string ProofCriteria,
    int SuggestedWeeks,
    DateOnly TargetDate
);

public record ConfirmMilestonesRequest(List<MilestoneConfirmItem> Milestones);

public record MilestoneConfirmItem(
    string Description,
    string ProofCriteria,
    DateOnly TargetDate
);

public record UpdateMilestoneRequest(string? Description, string? ProofCriteria, DateOnly? TargetDate);

public record MilestoneListItem(
    Guid Id,
    string Description,
    string ProofCriteria,
    DateOnly TargetDate,
    int SortOrder,
    string Status,
    DateTime? ProvedAt
);
