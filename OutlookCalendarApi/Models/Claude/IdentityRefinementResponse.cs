using System.Text.Json.Serialization;

namespace OutlookCalendarApi.Models.Claude;

public record IdentityRefinementResponse(
    [property: JsonPropertyName("refined_statement")] string RefinedStatement,
    [property: JsonPropertyName("explanation")] string Explanation
);
