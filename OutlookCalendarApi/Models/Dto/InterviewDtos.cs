namespace OutlookCalendarApi.Models.Dto;

// POST /api/interviews
public record StartInterviewRequest(string Type); // "identity-summit" | "milestone-sprint"

// POST /api/interviews/{id}/respond
public record InterviewRespondRequest(string Answer);

// Response from POST /api/interviews and POST /api/interviews/{id}/respond
public record InterviewStepResponse(
    Guid SessionId,
    Guid IdentityId,
    int CurrentStep,
    string Question // placeholder until Plan 02 wires up Claude
);

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
