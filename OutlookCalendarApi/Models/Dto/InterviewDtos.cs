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
    string? NextQuestion,        // present when IsComplete = false
    InterviewSummaryDto? Summary // present when IsComplete = true
);

public record InterviewSummaryDto(
    string IdentityStatement,
    string SummitDescription,
    string ProofCriteria,
    string? TargetDate,
    SummaryBreakdownDto[] Breakdown
);

public record SummaryBreakdownDto(string Component, string BasedOn);

// Response from POST /api/interviews/{id}/confirm
public record InterviewConfirmResponse(Guid IdentityId, Guid SummitId);

// GET /api/interviews/{id}
public record InterviewStateResponse(
    Guid SessionId,
    Guid? IdentityId,
    string Type,
    int CurrentStep,
    string ConversationHistory, // raw JSON string
    string AccumulatedData,     // raw JSON string
    bool Active,
    DateTime ExpiresAt
);
