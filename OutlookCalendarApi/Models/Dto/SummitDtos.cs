namespace OutlookCalendarApi.Models.Dto;

// POST /api/identities/{id}/summit
public record ConfirmSummitRequest(string Description, string ProofCriteria, DateOnly? TargetDate);
