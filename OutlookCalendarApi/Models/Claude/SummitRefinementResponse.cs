using System.Text.Json.Serialization;

namespace OutlookCalendarApi.Models.Claude;

public record SummitRefinementResponse(
    [property: JsonPropertyName("refined_goal")] string RefinedGoal,
    [property: JsonPropertyName("proof_criteria")] string ProofCriteria,
    [property: JsonPropertyName("explanation")] string Explanation
);
