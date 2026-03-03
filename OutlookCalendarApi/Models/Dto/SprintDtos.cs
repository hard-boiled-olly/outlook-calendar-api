namespace OutlookCalendarApi.Models.Dto;

public record SprintPlanGenerationResult(
    List<GeneratedHabitItem> Habits,
    List<GeneratedTaskItem> Tasks
);

public record GeneratedHabitItem(
    string Name,
    string Frequency,
    string Prescription,
    int DurationMins
);

public record GeneratedTaskItem(
    string Name,
    string Description,
    int SuggestedDaysFromStart,
    DateOnly Deadline,
    int DurationMins
);

public record ConfirmSprintPlanRequest(
    List<ConfirmHabitItem> Habits,
    List<ConfirmTaskItem> Tasks
);

public record ConfirmHabitItem(
    string Name,
    string Frequency,
    string Prescription,
    int DurationMins
);

public record ConfirmTaskItem(
    string Name,
    string Description,
    DateOnly Deadline,
    int DurationMins
);

public record SprintHabitDetail(
    Guid HabitId,
    string Name,
    string Frequency,
    Guid PrescriptionId,
    string Prescription,
    int DurationMins,
    List<HabitEventItem> Events
);

public record HabitEventItem(
    Guid Id,
    DateOnly ScheduledDate,
    TimeOnly? ScheduledTime,
    int DurationMins,
    string? CalendarEventId,
    string Status
);

public record SprintTaskDetail(
    Guid Id,
    string Name,
    string? Description,
    DateOnly Deadline,
    int? DurationMins,
    string? CalendarEventId,
    string Status
);

public record SprintConfirmResult(
    Guid SprintId,
    int CalendarEventsCreated
);
