using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using OutlookCalendarApi.Models.Dto;

namespace OutlookCalendarApi.Services;

public class GraphCalendarService
{
    private static GraphServiceClient CreateClient(string graphToken)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenProvider(graphToken));
        return new GraphServiceClient(authProvider);
    }

    public async Task<List<BusySlot>> GetScheduleAsync(
        string graphToken,
        string userEmail,
        DateOnly startDate,
        DateOnly endDate,
        string timeZone)
    {
        var client = CreateClient(graphToken);

        var requestBody = new Microsoft.Graph.Me.Calendar.GetSchedule.GetSchedulePostRequestBody
        {
            Schedules = [userEmail],
            StartTime = new DateTimeTimeZone
            {
                DateTime = startDate.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = timeZone
            },
            EndTime = new DateTimeTimeZone
            {
                DateTime = endDate.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = timeZone
            }
        };

        var response = await client.Me.Calendar.GetSchedule.PostAsGetSchedulePostResponseAsync(requestBody);

        var busySlots = new List<BusySlot>();

        if (response?.Value is not { } scheduleItems)
            return busySlots;

        foreach (var schedule in scheduleItems)
        {
            if (schedule.ScheduleItems is null) continue;

            foreach (var item in schedule.ScheduleItems)
            {
                if (item.Start?.DateTime is null || item.End?.DateTime is null) continue;

                var status = item.Status switch
                {
                    FreeBusyStatus.Busy => "busy",
                    FreeBusyStatus.Tentative => "tentative",
                    FreeBusyStatus.Oof => "oof",
                    FreeBusyStatus.WorkingElsewhere => "workingElsewhere",
                    _ => null
                };

                // Only include slots where the user is actually busy
                if (status is null) continue;

                var start = DateTimeOffset.Parse(item.Start.DateTime);
                var end = DateTimeOffset.Parse(item.End.DateTime);

                // If the Graph response includes timezone info, use it;
                // otherwise assume the timezone we requested
                if (start.Offset == TimeSpan.Zero && item.Start.TimeZone is null)
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
                    start = new DateTimeOffset(start.DateTime, tz.GetUtcOffset(start.DateTime));
                    end = new DateTimeOffset(end.DateTime, tz.GetUtcOffset(end.DateTime));
                }

                busySlots.Add(new BusySlot(start, end, status));
            }
        }

        return busySlots;
    }

    public async Task<string> CreateEventAsync(
        string graphToken,
        string subject,
        string bodyContent,
        DateOnly date,
        TimeOnly startTime,
        int durationMins,
        string timeZone)
    {
        var client = CreateClient(graphToken);

        var startDateTime = date.ToDateTime(startTime);
        var endDateTime = startDateTime.AddMinutes(durationMins);

        var calendarEvent = new Event
        {
            Subject = subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = bodyContent
            },
            Start = new DateTimeTimeZone
            {
                DateTime = startDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = timeZone
            },
            End = new DateTimeTimeZone
            {
                DateTime = endDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = timeZone
            },
            IsReminderOn = true,
            ReminderMinutesBeforeStart = 15
        };

        var created = await client.Me.Events.PostAsync(calendarEvent);
        return created?.Id ?? throw new InvalidOperationException("Graph did not return event ID");
    }

    public async Task<int> CreateHabitEventsAsync(
        string graphToken,
        string identityStatement,
        string habitName,
        string prescription,
        List<(Guid habitEventId, DateOnly date, TimeOnly startTime, int durationMins)> occurrences,
        string timeZone,
        Func<Guid, string, Task> onEventCreated)
    {
        int count = 0;
        foreach (var (habitEventId, date, startTime, durationMins) in occurrences)
        {
            var subject = $"[ProveIt] {habitName}";
            var body = $"{prescription}\n\nIdentity: {identityStatement}";

            var eventId = await CreateEventAsync(
                graphToken, subject, body, date, startTime, durationMins, timeZone);

            await onEventCreated(habitEventId, eventId);
            count++;

            await Task.Delay(250); // Respect Graph rate limit (4 req/sec/mailbox)
        }
        return count;
    }

    public async Task<int> CreateTaskEventsAsync(
        string graphToken,
        string identityStatement,
        List<(Guid taskId, string name, string? description, DateOnly deadline, TimeOnly startTime, int durationMins)> tasks,
        string timeZone,
        Func<Guid, string, Task> onEventCreated)
    {
        int count = 0;
        foreach (var (taskId, name, description, deadline, startTime, durationMins) in tasks)
        {
            var subject = $"[ProveIt] Task: {name}";
            var body = $"{description ?? name}\n\nIdentity: {identityStatement}";

            var eventId = await CreateEventAsync(
                graphToken, subject, body, deadline, startTime, durationMins, timeZone);

            await onEventCreated(taskId, eventId);
            count++;

            await Task.Delay(250);
        }
        return count;
    }

    public async Task<int> DeleteEventsAsync(string graphToken, List<string> calendarEventIds)
    {
        var client = CreateClient(graphToken);
        int deleted = 0;

        foreach (var eventId in calendarEventIds)
        {
            try
            {
                await client.Me.Events[eventId].DeleteAsync();
                deleted++;
                await Task.Delay(250); // Respect Graph rate limit
            }
            catch
            {
                // Event may already be deleted — continue with remaining
            }
        }

        return deleted;
    }

    private class TokenProvider(string token) : IAccessTokenProvider
    {
        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(token);

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();
    }
}
