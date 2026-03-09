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
