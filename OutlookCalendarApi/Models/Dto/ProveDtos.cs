namespace OutlookCalendarApi.Models.Dto;

// POST /api/milestones/{id}/prove — response
public record ProveMilestoneResult(
    Guid? NextMilestoneId,
    bool SummitAchieved,
    string? NextMilestoneDescription,
    string? NextMilestoneProofCriteria
);

// POST /api/milestones/{id}/miss — request
public record MissMilestoneRequest(string Reflection);

// POST /api/sprints/{id}/replan — request
public record ReplanRequest(string Reflection);

// POST /api/sprints/{id}/replan — response
// Returns merged habit list (existing + Claude updates) in same shape as SprintPlanGenerationResult
public record ReplanResult(
    string CoachingNote,
    List<GeneratedHabitItem> Habits,
    List<GeneratedTaskItem> NewTasks
);
