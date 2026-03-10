using System.Text.RegularExpressions;

namespace OutlookCalendarApi.Services;

/// <summary>
/// Distributes habit events across a date range based on frequency.
/// Shared between InterviewController (initial sprint) and PlanController (replan).
/// </summary>
public static class DateDistribution
{
    public static int ParseFrequency(string frequency)
    {
        var lower = frequency.ToLowerInvariant();
        if (lower.Contains("daily") || lower.Contains("every day")) return 7;
        var match = Regex.Match(lower, @"(\d+)\s*x?\s*(per|times|\/)\s*week");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 3;
    }

    public static List<DateOnly> DistributeEventsAcrossWeeks(
        DateOnly start, DateOnly end, int perWeek)
    {
        var dates = new List<DateOnly>();
        var current = start;

        while (current <= end)
        {
            var weekEnd = current.AddDays(6);
            if (weekEnd > end) weekEnd = end;

            var daysInWeek = weekEnd.DayNumber - current.DayNumber + 1;
            var spacing = Math.Max(1, daysInWeek / perWeek);

            for (int i = 0; i < perWeek; i++)
            {
                var date = current.AddDays(i * spacing);
                if (date > weekEnd) break;
                dates.Add(date);
            }

            current = current.AddDays(7);
        }

        return dates;
    }
}
