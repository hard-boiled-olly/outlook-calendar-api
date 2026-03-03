using System.Text.Json.Serialization;

namespace OutlookCalendarApi.Models.Claude;

public record SprintPlanResponse(
    [property: JsonPropertyName("habits")] List<HabitItem> Habits,
    [property: JsonPropertyName("tasks")] List<TaskItem> Tasks
);

public record HabitItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("frequency")] string Frequency,
    [property: JsonPropertyName("prescription")] string Prescription,
    [property: JsonPropertyName("duration_mins")] int DurationMins
);

public record TaskItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("suggested_days_from_start")] int SuggestedDaysFromStart,
    [property: JsonPropertyName("duration_mins")] int DurationMins
);
