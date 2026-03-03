namespace OutlookCalendarApi.Models.Dto;

// POST /api/identities/{id}/summit/refine
public record CreateSummitRequest(string RoughGoal);

// POST /api/identities/{id}/summit
public record ConfirmSummitRequest(string Description, string ProofCriteria, DateOnly? TargetDate);

// Response from /api/identities/{id}/summit/refine
public record SummitRefinementResult(string RefinedGoal, string ProofCriteria, string Explanation);
