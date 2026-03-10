namespace OutlookCalendarApi.Models.Dto;

// GET /api/preferences/scheduling
public record SchedulingPreferencesResponse(
    Guid Id,
    string WorkingHoursStart,                   // "09:00"
    string WorkingHoursEnd,                     // "17:30"
    Dictionary<string, string> PreferredTimes,  // { "exercise": "morning", "finance": "evening" }
    string TimeZone                             // "Europe/London"
);

// PUT /api/preferences/scheduling
public record UpdateSchedulingPreferencesRequest(
    string? WorkingHoursStart,  // "09:00"
    string? WorkingHoursEnd,    // "17:30"
    Dictionary<string, string>? PreferredTimes,
    string? TimeZone
);

// Represents a time block where the user is busy (from Graph getSchedule API)
public record BusySlot(
    DateTimeOffset Start,
    DateTimeOffset End,
    string Status  // "busy", "tentative", "oof" (out of office), "workingElsewhere"
);

// A single event that needs to be scheduled
public record SchedulingRequest(
    Guid EventId,          // HabitEvent or SprintTask ID
    string EventType,      // "habit" or "task"
    string Name,
    DateOnly Date,         // The day it should be scheduled on
    int DurationMins,
    string? ActivityType   // e.g. "exercise", "finance" — for preference matching
);

// The algorithm's output: where an event should go
public record ProposedSlot(
    Guid EventId,
    string EventType,
    string Name,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMins,
    bool IsPreferredTime,  // true if placed in the user's preferred window
    string? Note           // e.g. "Preferred morning slot was busy, scheduled at 14:00"
);

// POST /api/sprints/{id}/schedule/propose — response
public record ProposeScheduleResponse(
    List<ProposedSlot> Slots
);

// POST /api/sprints/{id}/schedule/confirm — request
public record ConfirmScheduleRequest(
    List<AcceptedSlot> Slots
);

// A slot the user has accepted (may differ from proposal if they adjusted times)
public record AcceptedSlot(
    Guid EventId,
    string EventType,      // "habit" or "task"
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMins
);

// POST /api/sprints/{id}/schedule/confirm — response
public record ConfirmScheduleResponse(
    int CalendarEventsCreated,
    int Failed,
    List<string> Errors
);

// Result of a batch Graph API operation (create or delete events)
public record BatchResult(int Succeeded, int Failed, List<string> Errors);
