namespace OutlookCalendarApi.Models.Dto;

// POST /api/interviews
public record StartInterviewRequest(string Type); // "identity-summit" | "milestone-sprint"

// POST /api/interviews/{id}/respond
public record InterviewRespondRequest(string Answer);

// Response from POST /api/interviews (start) — always returns first question
public record InterviewStepResponse(
    Guid SessionId,
    Guid IdentityId,
    int CurrentStep,
    string Question
);

// Response from POST /api/interviews/{id}/respond
public record InterviewRespondResponse(
    Guid SessionId,
    bool IsComplete,
    string? NextQuestion,                        // present when IsComplete = false
    InterviewSummaryDto? Summary,                // present when type = identity-summit
    MilestoneSprintSummaryDto? MilestoneSprintSummary // present when type = milestone-sprint
);

public record InterviewSummaryDto(
    string IdentityStatement,
    string SummitDescription,
    string ProofCriteria,
    string? TargetDate,
    SummaryBreakdownDto[] Breakdown
);

public record SummaryBreakdownDto(string Component, string BasedOn);

// POST /api/interviews/{id}/confirm
public record ConfirmInterviewRequest(string? TargetDate); // ISO 8601 or null to clear

// Response from POST /api/interviews/{id}/confirm
public record InterviewConfirmResponse(Guid IdentityId, Guid SummitId);

// POST /api/interviews/milestone-sprint
public record StartMilestoneSprintRequest(Guid IdentityId, Guid SummitId);

// Summary shown when milestone-sprint interview completes
public record MilestoneSprintSummaryDto(
    MilestoneSummaryItem[] Milestones,
    HabitSummaryItem[] FirstSprintHabits,
    TaskSummaryItem[] FirstSprintTasks,
    SummaryBreakdownDto[] Breakdown
);

public record MilestoneSummaryItem(string Description, string ProofCriteria, string TargetDate, int SortOrder);
public record HabitSummaryItem(string Name, string Frequency, string Prescription, int DurationMins);
public record TaskSummaryItem(string Name, string Description, string Deadline, int DurationMins);

// Response from confirming milestone-sprint interview
public record MilestoneSprintConfirmResponse(Guid[] MilestoneIds, Guid SprintId);

// GET /api/interviews/{id}
public record InterviewStateResponse(
    Guid SessionId,
    Guid? IdentityId,
    Guid? SummitId,
    string Type,
    int CurrentStep,
    string ConversationHistory, // raw JSON string
    string AccumulatedData,     // raw JSON string
    bool Active,
    DateTime ExpiresAt
);
