using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using OutlookCalendarApi.Models.Dto;

namespace OutlookCalendarApi.Services;

public class GraphCalendarService(ILogger<GraphCalendarService> logger)
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

    private async Task<(List<string?> createdIds, BatchResult result)> CreateEventsInBatchesAsync(
        GraphServiceClient client,
        List<Event> events)
    {
        const int batchSize = 20;
        var createdEventIds = new List<string?>(events.Count);
        var errors = new List<string>();
        var failed = 0;

        for (var batchStart = 0; batchStart < events.Count; batchStart += batchSize)
        {
            var batch = new BatchRequestContentCollection(client);
            var requestIds = new List<string>();

            var batchEnd = Math.Min(batchStart + batchSize, events.Count);
            for (var i = batchStart; i < batchEnd; i++)
            {
                var request = client.Me.Events.ToPostRequestInformation(events[i]);
                var requestId = await batch.AddBatchRequestStepAsync(request);
                requestIds.Add(requestId);
            }

            var response = await client.Batch.PostAsync(batch);

            foreach (var requestId in requestIds)
            {
                try
                {
                    var eventResponse = await response!.GetResponseByIdAsync<Event>(requestId);
                    if (eventResponse?.Id is { } id)
                    {
                        createdEventIds.Add(id);
                    }
                    else
                    {
                        failed++;
                        errors.Add("Graph returned event with no ID");
                        createdEventIds.Add(null);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add(ex.Message);
                    createdEventIds.Add(null);
                    logger.LogWarning(ex, "Failed to create calendar event in batch");
                }
            }
        }

        return (createdEventIds, new BatchResult(createdEventIds.Count - failed, failed, errors));
    }

    private async Task<BatchResult> DeleteEventsInBatchesAsync(
        GraphServiceClient client,
        List<string> calendarEventIds)
    {
        const int batchSize = 20;
        var deleted = 0;
        var failed = 0;
        var errors = new List<string>();

        for (var batchStart = 0; batchStart < calendarEventIds.Count; batchStart += batchSize)
        {
            var batch = new BatchRequestContentCollection(client);
            var requestIds = new List<string>();

            var batchEnd = Math.Min(batchStart + batchSize, calendarEventIds.Count);
            for (var i = batchStart; i < batchEnd; i++)
            {
                var request = client.Me.Events[calendarEventIds[i]].ToDeleteRequestInformation();
                var requestId = await batch.AddBatchRequestStepAsync(request);
                requestIds.Add(requestId);
            }

            var response = await client.Batch.PostAsync(batch);
            var statusCodes = await response!.GetResponsesStatusCodesAsync();

            foreach (var requestId in requestIds)
            {
                if (statusCodes.TryGetValue(requestId, out var status))
                {
                    if (status is System.Net.HttpStatusCode.NoContent or System.Net.HttpStatusCode.NotFound)
                        deleted++;
                    else
                    {
                        failed++;
                        errors.Add($"Delete returned {status}");
                        logger.LogWarning("Failed to delete calendar event, status: {Status}", status);
                    }
                }
                else
                {
                    failed++;
                    errors.Add("No status returned for delete request");
                }
            }
        }

        return new BatchResult(deleted, failed, errors);
    }

    public async Task<BatchResult> CreateHabitEventsAsync(
        string graphToken,
        string identityStatement,
        string habitName,
        string prescription,
        List<(Guid habitEventId, DateOnly date, TimeOnly startTime, int durationMins)> occurrences,
        string timeZone,
        Func<Guid, string, Task> onEventCreated)
    {
        var client = CreateClient(graphToken);
        var events = new List<Event>(occurrences.Count);
        var habitEventIds = new List<Guid>(occurrences.Count);

        foreach (var (habitEventId, date, startTime, durationMins) in occurrences)
        {
            var startDateTime = date.ToDateTime(startTime);
            var endDateTime = startDateTime.AddMinutes(durationMins);

            events.Add(new Event
            {
                Subject = $"[ProveIt] {habitName}",
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = $"{prescription}\n\nIdentity: {identityStatement}"
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
            });
            habitEventIds.Add(habitEventId);
        }

        var (createdIds, result) = await CreateEventsInBatchesAsync(client, events);

        for (var i = 0; i < createdIds.Count; i++)
        {
            if (createdIds[i] is { } calendarEventId)
                await onEventCreated(habitEventIds[i], calendarEventId);
        }

        return result;
    }

    public async Task<BatchResult> CreateTaskEventsAsync(
        string graphToken,
        string identityStatement,
        List<(Guid taskId, string name, string? description, DateOnly deadline, TimeOnly startTime, int durationMins)> tasks,
        string timeZone,
        Func<Guid, string, Task> onEventCreated)
    {
        var client = CreateClient(graphToken);
        var events = new List<Event>(tasks.Count);
        var taskIds = new List<Guid>(tasks.Count);

        foreach (var (taskId, name, description, deadline, startTime, durationMins) in tasks)
        {
            var startDateTime = deadline.ToDateTime(startTime);
            var endDateTime = startDateTime.AddMinutes(durationMins);

            events.Add(new Event
            {
                Subject = $"[ProveIt] Task: {name}",
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = $"{description ?? name}\n\nIdentity: {identityStatement}"
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
            });
            taskIds.Add(taskId);
        }

        var (createdIds, result) = await CreateEventsInBatchesAsync(client, events);

        for (var i = 0; i < createdIds.Count; i++)
        {
            if (createdIds[i] is { } calendarEventId)
                await onEventCreated(taskIds[i], calendarEventId);
        }

        return result;
    }

    public async Task<BatchResult> DeleteEventsAsync(string graphToken, List<string> calendarEventIds)
    {
        var client = CreateClient(graphToken);
        return await DeleteEventsInBatchesAsync(client, calendarEventIds);
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
