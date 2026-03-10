using OutlookCalendarApi.Models.Domain;
using OutlookCalendarApi.Models.Dto;

namespace OutlookCalendarApi.Services;

public class SchedulingService
{
    // Maps preference labels to time windows within working hours
    private static (TimeOnly start, TimeOnly end) GetPreferredWindow(
        string preference, TimeOnly workStart, TimeOnly workEnd)
    {
        // Split working hours into three roughly equal windows
        var totalMinutes = (workEnd.ToTimeSpan() - workStart.ToTimeSpan()).TotalMinutes;
        var thirdMinutes = (int)(totalMinutes / 3);

        return preference.ToLowerInvariant() switch
        {
            "morning" => (workStart, workStart.AddMinutes(thirdMinutes)),
            "afternoon" => (workStart.AddMinutes(thirdMinutes), workStart.AddMinutes(thirdMinutes * 2)),
            "evening" => (workStart.AddMinutes(thirdMinutes * 2), workEnd),
            _ => (workStart, workEnd) // No preference — full working hours
        };
    }

    public List<ProposedSlot> FindAvailableSlots(
        List<BusySlot> busySlots,
        SchedulingPreferences preferences,
        List<SchedulingRequest> requests)
    {
        var results = new List<ProposedSlot>();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(preferences.TimeZone);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var today = DateOnly.FromDateTime(now.DateTime);

        // Track slots we've already proposed (so we don't double-book ourselves)
        var selfBookedSlots = new List<(DateOnly date, TimeOnly start, TimeOnly end)>();

        // Group requests by date so we can schedule each day's events together
        var byDate = requests
            .GroupBy(r => r.Date)
            .OrderBy(g => g.Key);

        foreach (var dayGroup in byDate)
        {
            var date = dayGroup.Key;

            // Convert busy slots to TimeOnly ranges for this specific day
            var busyRanges = GetBusyRangesForDate(busySlots, date, tz);

            // Add our own previously proposed slots as busy too
            foreach (var self in selfBookedSlots.Where(s => s.date == date))
                busyRanges.Add((self.start, self.end));

            // Sort events: preferred-time events first, then by duration (longest first)
            // This gives preferred-time events priority for their windows
            var sorted = dayGroup
                .OrderByDescending(r => r.ActivityType is not null)
                .ThenByDescending(r => r.DurationMins)
                .ToList();

            foreach (var request in sorted)
            {
                var slot = FindSlotForEvent(
                    request, date, today, now.TimeOfDay, busyRanges, preferences);

                results.Add(slot);

                // If we placed it, mark that time as busy for subsequent events
                if (slot.Note is not "Could not find an available slot")
                {
                    var endTime = slot.StartTime.AddMinutes(slot.DurationMins);
                    busyRanges.Add((slot.StartTime, endTime));
                    selfBookedSlots.Add((date, slot.StartTime, endTime));
                }
            }
        }

        return results;
    }

    private ProposedSlot FindSlotForEvent(
        SchedulingRequest request,
        DateOnly date,
        DateOnly today,
        TimeSpan currentTime,
        List<(TimeOnly start, TimeOnly end)> busyRanges,
        SchedulingPreferences preferences)
    {
        var workStart = preferences.WorkingHoursStart;
        var workEnd = preferences.WorkingHoursEnd;

        // If it's today, don't schedule before the current time (+ 15 min buffer)
        var earliestStart = workStart;
        if (date == today)
        {
            var nowPlusBuffer = TimeOnly.FromTimeSpan(currentTime).AddMinutes(15);
            if (nowPlusBuffer > earliestStart)
                earliestStart = RoundUpTo15(nowPlusBuffer);
        }

        // If the earliest possible start is past working hours, we can't schedule today
        if (earliestStart >= workEnd)
        {
            return new ProposedSlot(
                request.EventId, request.EventType, request.Name,
                date, workStart, request.DurationMins,
                false, "Could not find an available slot");
        }

        // Determine preferred window
        string? pref = null;
        var hasPreference = request.ActivityType is not null
            && preferences.PreferredTimes.TryGetValue(request.ActivityType, out pref);
        var (prefStart, prefEnd) = hasPreference
            ? GetPreferredWindow(pref!, workStart, workEnd)
            : (workStart, workEnd);

        // Clamp preferred window to earliest possible start
        if (prefStart < earliestStart)
            prefStart = earliestStart;

        // Try preferred window first
        var slot = FindFirstGap(prefStart, prefEnd, request.DurationMins, busyRanges);

        if (slot is not null)
        {
            return new ProposedSlot(
                request.EventId, request.EventType, request.Name,
                date, slot.Value, request.DurationMins,
                true, null);
        }

        // Fall back to any available slot in working hours
        slot = FindFirstGap(earliestStart, workEnd, request.DurationMins, busyRanges);

        if (slot is not null)
        {
            var note = hasPreference
                ? $"Preferred {pref} slot was busy, scheduled at {slot.Value:HH:mm}"
                : null;

            return new ProposedSlot(
                request.EventId, request.EventType, request.Name,
                date, slot.Value, request.DurationMins,
                false, note);
        }

        // No slot available
        return new ProposedSlot(
            request.EventId, request.EventType, request.Name,
            date, workStart, request.DurationMins,
            false, "Could not find an available slot");
    }

    private static TimeOnly? FindFirstGap(
        TimeOnly windowStart,
        TimeOnly windowEnd,
        int durationMins,
        List<(TimeOnly start, TimeOnly end)> busyRanges)
    {
        // Sort busy ranges by start time
        var sorted = busyRanges
            .Where(b => b.end > windowStart && b.start < windowEnd)
            .OrderBy(b => b.start)
            .ToList();

        var candidate = windowStart;

        foreach (var (busyStart, busyEnd) in sorted)
        {
            // Is there enough room before this busy block?
            if (candidate.AddMinutes(durationMins) <= busyStart)
                return candidate;

            // Move past this busy block
            if (busyEnd > candidate)
                candidate = RoundUpTo15(busyEnd);
        }

        // Check if there's room after the last busy block
        if (candidate.AddMinutes(durationMins) <= windowEnd)
            return candidate;

        return null;
    }

    private static List<(TimeOnly start, TimeOnly end)> GetBusyRangesForDate(
        List<BusySlot> busySlots,
        DateOnly date,
        TimeZoneInfo tz)
    {
        var ranges = new List<(TimeOnly start, TimeOnly end)>();

        foreach (var slot in busySlots)
        {
            var localStart = TimeZoneInfo.ConvertTime(slot.Start, tz);
            var localEnd = TimeZoneInfo.ConvertTime(slot.End, tz);

            var slotStartDate = DateOnly.FromDateTime(localStart.DateTime);
            var slotEndDate = DateOnly.FromDateTime(localEnd.DateTime);

            // Check if this busy slot overlaps with our target date
            if (slotStartDate > date || slotEndDate < date)
                continue;

            // Clamp to midnight boundaries of this date
            var timeStart = slotStartDate == date
                ? TimeOnly.FromDateTime(localStart.DateTime)
                : TimeOnly.MinValue;

            var timeEnd = slotEndDate == date
                ? TimeOnly.FromDateTime(localEnd.DateTime)
                : TimeOnly.MaxValue;

            ranges.Add((timeStart, timeEnd));
        }

        return ranges;
    }

    private static TimeOnly RoundUpTo15(TimeOnly time)
    {
        var mins = time.Minute;
        var remainder = mins % 15;
        if (remainder == 0) return time;
        return time.AddMinutes(15 - remainder);
    }
}
