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
