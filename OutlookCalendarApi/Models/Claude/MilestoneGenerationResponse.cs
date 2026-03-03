using System.Text.Json.Serialization;

namespace OutlookCalendarApi.Models.Claude;

public record MilestoneGenerationResponse(
    [property: JsonPropertyName("milestones")] List<MilestoneItem> Milestones
);

public record MilestoneItem(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("proof_criteria")] string ProofCriteria,
    [property: JsonPropertyName("suggested_weeks")] int SuggestedWeeks
);
