using System.Text.Json.Serialization;

namespace OutlookCalendarApi.Models.Claude;

public record InterviewOutput(
    [property: JsonPropertyName("identity_statement")] string IdentityStatement,
    [property: JsonPropertyName("summit_description")] string SummitDescription,
    [property: JsonPropertyName("proof_criteria")] string ProofCriteria,
    [property: JsonPropertyName("target_date")] string? TargetDate,
    [property: JsonPropertyName("summary_breakdown")] SummaryBreakdownItem[] SummaryBreakdown
);

public record SummaryBreakdownItem(
    [property: JsonPropertyName("component")] string Component,
    [property: JsonPropertyName("based_on")] string BasedOn
);
