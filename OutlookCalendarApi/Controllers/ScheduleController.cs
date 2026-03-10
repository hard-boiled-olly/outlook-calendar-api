using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Models.Domain;
using OutlookCalendarApi.Models.Dto;
using OutlookCalendarApi.Services;

namespace OutlookCalendarApi.Controllers;

[ApiController]
[Authorize]
public class ScheduleController(
    AppDbContext db,
    GraphCalendarService graph,
    SchedulingService scheduling) : ControllerBase
{
    // POST /api/sprints/{id}/schedule/propose
    // Fetches calendar free/busy data, runs the scheduling algorithm,
    // and returns proposed time slots for all habit events and tasks in the sprint.
    [HttpPost("api/sprints/{sprintId:guid}/schedule/propose")]
    public async Task<IActionResult> Propose(Guid sprintId)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var sprint = await db.Sprints
            .Include(s => s.Milestone)
            .ThenInclude(m => m.Summit)
            .ThenInclude(s => s.Identity)
            .FirstOrDefaultAsync(s => s.Id == sprintId && s.Milestone.Summit.Identity.UserId == userId);

        if (sprint == null)
            return NotFound();

        var graphToken = Request.Headers["X-Graph-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(graphToken))
            return BadRequest("X-Graph-Token header required");

        // Load user email and scheduling preferences
        var user = await db.Users.FindAsync(userId);
        if (user == null) return NotFound("User not found");

        var preferences = await db.SchedulingPreferences
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        // Use sensible defaults if no preferences saved yet
        preferences ??= new SchedulingPreferences
        {
            UserId = userId,
            TimeZone = "Europe/London"
        };

        // Determine sprint date range
        var sprintStart = DateOnly.FromDateTime(sprint.StartedAt);
        var sprintEnd = sprint.Milestone.TargetDate;
        var maxEnd = sprintStart.AddDays(28);
        if (sprintEnd > maxEnd) sprintEnd = maxEnd;

        // Fetch free/busy from Graph
        var busySlots = await graph.GetScheduleAsync(
            graphToken, user.Email, sprintStart, sprintEnd, preferences.TimeZone);

        // Build scheduling requests from habit events and tasks
        var requests = new List<SchedulingRequest>();

        var habitEvents = await db.HabitEvents
            .Where(he => he.SprintId == sprintId && he.CalendarEventId == null)
            .Include(he => he.HabitPrescription)
            .ThenInclude(hp => hp.Habit)
            .ToListAsync();

        foreach (var he in habitEvents)
        {
            requests.Add(new SchedulingRequest(
                he.Id, "habit", he.HabitPrescription.Habit.Name,
                he.ScheduledDate, he.DurationMins, null));
        }

        var tasks = await db.SprintTasks
            .Where(st => st.SprintId == sprintId && st.CalendarEventId == null && st.DurationMins != null)
            .ToListAsync();

        foreach (var task in tasks)
        {
            requests.Add(new SchedulingRequest(
                task.Id, "task", task.Name,
                task.Deadline, task.DurationMins ?? 30, null));
        }

        // Run scheduling algorithm
        var proposedSlots = scheduling.FindAvailableSlots(busySlots, preferences, requests);

        return Ok(new ProposeScheduleResponse(proposedSlots));
    }

    // POST /api/sprints/{id}/schedule/confirm
    // Creates Outlook calendar events at the accepted times and updates the database.
    [HttpPost("api/sprints/{sprintId:guid}/schedule/confirm")]
    public async Task<IActionResult> Confirm(
        Guid sprintId, [FromBody] ConfirmScheduleRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var sprint = await db.Sprints
            .Include(s => s.Milestone)
            .ThenInclude(m => m.Summit)
            .ThenInclude(s => s.Identity)
            .FirstOrDefaultAsync(s => s.Id == sprintId && s.Milestone.Summit.Identity.UserId == userId);

        if (sprint == null)
            return NotFound();

        var graphToken = Request.Headers["X-Graph-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(graphToken))
            return BadRequest("X-Graph-Token header required");

        var identity = sprint.Milestone.Summit.Identity;

        var preferences = await db.SchedulingPreferences
            .FirstOrDefaultAsync(sp => sp.UserId == userId);
        var timeZone = preferences?.TimeZone ?? "Europe/London";

        var totalEvents = 0;

        // Group accepted slots by type
        var habitSlots = request.Slots.Where(s => s.EventType == "habit").ToList();
        var taskSlots = request.Slots.Where(s => s.EventType == "task").ToList();

        // Create calendar events for habits
        // Group by prescription so we can pass habit name and prescription text
        var habitEventIds = habitSlots.Select(s => s.EventId).ToHashSet();
        var habitEventsDb = await db.HabitEvents
            .Where(he => habitEventIds.Contains(he.Id))
            .Include(he => he.HabitPrescription)
            .ThenInclude(hp => hp.Habit)
            .ToListAsync();

        // Group by prescription for batch creation
        var byPrescription = habitEventsDb
            .GroupBy(he => he.HabitPrescriptionId)
            .ToList();

        foreach (var group in byPrescription)
        {
            var first = group.First();
            var slotLookup = habitSlots.ToDictionary(s => s.EventId);

            var occurrences = group
                .Where(he => slotLookup.ContainsKey(he.Id))
                .Select(he =>
                {
                    var slot = slotLookup[he.Id];
                    return (he.Id, slot.Date, slot.StartTime, slot.DurationMins);
                })
                .ToList();

            totalEvents += await graph.CreateHabitEventsAsync(
                graphToken, identity.Statement,
                first.HabitPrescription.Habit.Name,
                first.HabitPrescription.Prescription,
                occurrences, timeZone,
                async (habitEventId, calendarEventId) =>
                {
                    var he = await db.HabitEvents.FindAsync(habitEventId);
                    if (he != null)
                    {
                        he.ScheduledTime = slotLookup[habitEventId].StartTime;
                        he.CalendarEventId = calendarEventId;
                        he.Status = "synced";
                    }
                });
        }

        // Create calendar events for tasks
        var taskSlotLookup = taskSlots.ToDictionary(s => s.EventId);
        var taskIds = taskSlots.Select(s => s.EventId).ToHashSet();
        var tasksDb = await db.SprintTasks
            .Where(st => taskIds.Contains(st.Id))
            .ToListAsync();

        var taskOccurrences = tasksDb
            .Where(st => taskSlotLookup.ContainsKey(st.Id))
            .Select(st =>
            {
                var slot = taskSlotLookup[st.Id];
                return (st.Id, st.Name, st.Description, slot.Date, slot.StartTime, slot.DurationMins);
            })
            .ToList();

        totalEvents += await graph.CreateTaskEventsAsync(
            graphToken, identity.Statement,
            taskOccurrences, timeZone,
            async (taskId, calendarEventId) =>
            {
                var st = await db.SprintTasks.FindAsync(taskId);
                if (st != null)
                {
                    st.CalendarEventId = calendarEventId;
                    st.Status = "synced";
                }
            });

        await db.SaveChangesAsync();

        return Ok(new ConfirmScheduleResponse(totalEvents));
    }
}
