using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;

namespace OutlookCalendarApi.Services;

public class GraphCalendarService
{
    private static GraphServiceClient CreateClient(string graphToken)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenProvider(graphToken));
        return new GraphServiceClient(authProvider);
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
        List<(Guid habitEventId, DateOnly date, int durationMins)> occurrences,
        string timeZone,
        Func<Guid, string, Task> onEventCreated)
    {
        int count = 0;
        foreach (var (habitEventId, date, durationMins) in occurrences)
        {
            var startTime = new TimeOnly(9, 0);
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
        List<(Guid taskId, string name, string? description, DateOnly deadline, int durationMins)> tasks,
        string timeZone,
        Func<Guid, string, Task> onEventCreated)
    {
        int count = 0;
        foreach (var (taskId, name, description, deadline, durationMins) in tasks)
        {
            var startTime = new TimeOnly(10, 0);
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
