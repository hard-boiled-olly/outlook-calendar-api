using System.Text.Json.Serialization;

namespace OutlookCalendarApi.Models.Claude;

public record MilestoneSprintOutput(
    [property: JsonPropertyName("milestones")] MilestoneOutputItem[] Milestones,
    [property: JsonPropertyName("first_sprint_habits")] HabitOutputItem[] FirstSprintHabits,
    [property: JsonPropertyName("first_sprint_tasks")] TaskOutputItem[] FirstSprintTasks,
    [property: JsonPropertyName("plan_breakdown")] SummaryBreakdownItem[] PlanBreakdown,
    [property: JsonPropertyName("scheduling_preferences")] SchedulingPreferencesOutput? SchedulingPreferences
);

public record SchedulingPreferencesOutput(
    [property: JsonPropertyName("working_hours_start")] string? WorkingHoursStart,
    [property: JsonPropertyName("working_hours_end")] string? WorkingHoursEnd,
    [property: JsonPropertyName("preferred_times")] PreferredTimeEntry[]? PreferredTimes
);

public record PreferredTimeEntry(
    [property: JsonPropertyName("activity")] string Activity,
    [property: JsonPropertyName("time_slot")] string TimeSlot
);

public record MilestoneOutputItem(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("proof_criteria")] string ProofCriteria,
    [property: JsonPropertyName("target_date")] string TargetDate,
    [property: JsonPropertyName("sort_order")] int SortOrder
);

public record HabitOutputItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("frequency")] string Frequency,
    [property: JsonPropertyName("prescription")] string Prescription,
    [property: JsonPropertyName("duration_mins")] int DurationMins
);

public record TaskOutputItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("deadline")] string Deadline,
    [property: JsonPropertyName("duration_mins")] int DurationMins
);
