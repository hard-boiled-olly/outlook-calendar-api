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
public class PlanController(AppDbContext db, ClaudeService claude, GraphCalendarService graph) : ControllerBase
{
    [HttpPost("api/milestones/{milestoneId:guid}/plan")]
    public async Task<IActionResult> GeneratePlan(Guid milestoneId)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var milestone = await db.Milestones
            .Include(m => m.Summit)
            .ThenInclude(s => s.Identity)
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.Summit.Identity.UserId == userId);

        if (milestone == null)
            return NotFound();

        if (milestone.Status != "active")
            return BadRequest("Only active milestones can be planned");

        if (await db.Sprints.AnyAsync(s => s.MilestoneId == milestoneId && s.Status == "active"))
            return Conflict("A sprint is already active for this milestone");

        var prevSprint = await db.Sprints
            .Where(s => s.MilestoneId == milestoneId && s.Reflection != null)
            .OrderByDescending(s => s.SprintNumber)
            .FirstOrDefaultAsync();

        var result = await claude.GenerateSprintPlanAsync(
            milestone.Summit.Identity.Statement,
            milestone.Summit.Description,
            milestone.Description,
            milestone.ProofCriteria,
            prevSprint?.Reflection);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var habits = result.Habits.Select(h => new GeneratedHabitItem(
            h.Name, h.Frequency, h.Prescription, h.DurationMins
        )).ToList();

        var tasks = result.Tasks.Select(t => new GeneratedTaskItem(
            t.Name, t.Description, t.SuggestedDaysFromStart,
            today.AddDays(t.SuggestedDaysFromStart), t.DurationMins
        )).ToList();

        return Ok(new SprintPlanGenerationResult(habits, tasks));
    }

    [HttpPost("api/milestones/{milestoneId:guid}/plan/confirm")]
    public async Task<IActionResult> ConfirmPlan(Guid milestoneId, [FromBody] ConfirmSprintPlanRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var milestone = await db.Milestones
            .Include(m => m.Summit)
            .ThenInclude(s => s.Identity)
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.Summit.Identity.UserId == userId);

        if (milestone == null)
            return NotFound();

        var graphToken = Request.Headers["X-Graph-Token"].FirstOrDefault();

        var identity = milestone.Summit.Identity;

        // Create sprint
        var sprintNumber = await db.Sprints.CountAsync(s => s.MilestoneId == milestoneId) + 1;
        var sprint = new Sprint
        {
            MilestoneId = milestoneId,
            SprintNumber = sprintNumber,
            StartedAt = DateTime.UtcNow,
            Status = "active"
        };
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync();

        var sprintStart = DateOnly.FromDateTime(DateTime.UtcNow);
        var sprintEnd = milestone.TargetDate;
        var maxEnd = sprintStart.AddDays(28);
        if (sprintEnd > maxEnd) sprintEnd = maxEnd;

        // Create habits, prescriptions, and events
        foreach (var item in request.Habits)
        {
            var habit = await db.Habits.FirstOrDefaultAsync(
                h => h.IdentityId == identity.Id && h.Name == item.Name);

            if (habit == null)
            {
                habit = new Habit
                {
                    IdentityId = identity.Id,
                    Name = item.Name,
                    Frequency = item.Frequency
                };
                db.Habits.Add(habit);
                await db.SaveChangesAsync();
            }

            var prescription = new HabitPrescription
            {
                HabitId = habit.Id,
                SprintId = sprint.Id,
                Prescription = item.Prescription
            };
            db.HabitPrescriptions.Add(prescription);
            await db.SaveChangesAsync();

            var eventsPerWeek = DateDistribution.ParseFrequency(item.Frequency);
            var dates = DateDistribution.DistributeEventsAcrossWeeks(sprintStart, sprintEnd, eventsPerWeek);

            foreach (var date in dates)
            {
                db.HabitEvents.Add(new HabitEvent
                {
                    HabitPrescriptionId = prescription.Id,
                    SprintId = sprint.Id,
                    ScheduledDate = date,
                    DurationMins = item.DurationMins,
                    Status = "pending"
                });
            }
        }

        // Create sprint tasks
        foreach (var item in request.Tasks)
        {
            db.SprintTasks.Add(new SprintTask
            {
                SprintId = sprint.Id,
                Name = item.Name,
                Description = item.Description,
                Deadline = item.Deadline,
                DurationMins = item.DurationMins,
                Status = "pending"
            });
        }

        await db.SaveChangesAsync();

        // Create calendar events via Graph (only if token provided — otherwise
        // the scheduling flow handles event creation separately)
        var totalEvents = 0;

        if (!string.IsNullOrEmpty(graphToken))
        {
            var timeZone = "UTC";

            // Habit events
            var prescriptions = await db.HabitPrescriptions
                .Where(hp => hp.SprintId == sprint.Id)
                .Include(hp => hp.Habit)
                .Include(hp => hp.HabitEvents.Where(he => he.SprintId == sprint.Id))
                .ToListAsync();

            foreach (var prescription in prescriptions)
            {
                var occurrences = prescription.HabitEvents
                    .Select(he => (he.Id, he.ScheduledDate,
                        he.ScheduledTime ?? new TimeOnly(9, 0), he.DurationMins))
                    .ToList();

                totalEvents += await graph.CreateHabitEventsAsync(
                    graphToken, identity.Statement,
                    prescription.Habit.Name, prescription.Prescription,
                    occurrences, timeZone,
                    async (habitEventId, calendarEventId) =>
                    {
                        var he = await db.HabitEvents.FindAsync(habitEventId);
                        if (he != null)
                        {
                            he.CalendarEventId = calendarEventId;
                            he.Status = "synced";
                        }
                    });
            }

            // Sprint task events
            var sprintTasks = await db.SprintTasks
                .Where(st => st.SprintId == sprint.Id && st.DurationMins != null)
                .ToListAsync();

            var taskOccurrences = sprintTasks
                .Select(st => (st.Id, st.Name, st.Description, st.Deadline,
                    new TimeOnly(10, 0), st.DurationMins ?? 30))
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
        }

        return Ok(new SprintConfirmResult(sprint.Id, totalEvents));
    }

    [HttpPost("api/sprints/{sprintId:guid}/replan")]
    public async Task<IActionResult> Replan(Guid sprintId, [FromBody] ReplanRequest request)
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

        // Save reflection on this sprint
        sprint.Reflection = request.Reflection;
        await db.SaveChangesAsync();

        var identity = sprint.Milestone.Summit.Identity;
        var summit = sprint.Milestone.Summit;
        var provedMilestone = sprint.Milestone;

        // Find the next active milestone
        var nextMilestone = await db.Milestones
            .Where(m => m.SummitId == summit.Id && m.Status == "active")
            .OrderBy(m => m.SortOrder)
            .FirstOrDefaultAsync();

        if (nextMilestone == null)
            return BadRequest("No active milestone to replan towards");

        // Get current habits for this identity
        var currentHabits = await db.HabitPrescriptions
            .Where(hp => hp.SprintId == sprintId)
            .Include(hp => hp.Habit)
            .Include(hp => hp.HabitEvents.Where(he => he.SprintId == sprintId))
            .ToListAsync();

        var habitsDescription = string.Join("; ", currentHabits.Select(
            hp => $"{hp.Habit.Name} ({hp.Habit.Frequency}): {hp.Prescription} [id:{hp.HabitId}]"));

        // Call Claude for replan
        var result = await claude.ReplanAsync(
            identity.Statement,
            summit.Description,
            provedMilestone.Description,
            nextMilestone.Description,
            nextMilestone.ProofCriteria,
            habitsDescription,
            request.Reflection);

        // Merge: apply Claude's updated prescriptions to existing habits
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var updatedLookup = result.UpdatedPrescriptions
            .ToDictionary(u => u.HabitId, u => u.NewPrescription);

        var mergedHabits = currentHabits.Select(hp =>
        {
            var newPrescription = updatedLookup.TryGetValue(hp.HabitId.ToString(), out var p)
                ? p : hp.Prescription;
            return new GeneratedHabitItem(
                hp.Habit.Name, hp.Habit.Frequency, newPrescription,
                hp.HabitEvents.FirstOrDefault()?.DurationMins ?? 30);
        }).ToList();

        var newTasks = result.NewTasks.Select(t => new GeneratedTaskItem(
            t.Name, t.Description, t.SuggestedDaysFromStart,
            today.AddDays(t.SuggestedDaysFromStart), t.DurationMins
        )).ToList();

        return Ok(new ReplanResult(result.CoachingNote, mergedHabits, newTasks));
    }

    [HttpGet("api/sprints/{sprintId:guid}/habits")]
    public async Task<IActionResult> GetHabits(Guid sprintId)
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

        var prescriptions = await db.HabitPrescriptions
            .Where(hp => hp.SprintId == sprintId)
            .Include(hp => hp.Habit)
            .Include(hp => hp.HabitEvents.Where(he => he.SprintId == sprintId))
            .ToListAsync();

        var items = prescriptions.Select(hp => new SprintHabitDetail(
            hp.HabitId, hp.Habit.Name, hp.Habit.Frequency,
            hp.Id, hp.Prescription, hp.HabitEvents.FirstOrDefault()?.DurationMins ?? 0,
            hp.HabitEvents.OrderBy(he => he.ScheduledDate).Select(he => new HabitEventItem(
                he.Id, he.ScheduledDate, he.ScheduledTime,
                he.DurationMins, he.CalendarEventId, he.Status
            )).ToList()
        )).ToList();

        return Ok(items);
    }

    [HttpGet("api/sprints/{sprintId:guid}/tasks")]
    public async Task<IActionResult> GetTasks(Guid sprintId)
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

        var tasks = await db.SprintTasks
            .Where(st => st.SprintId == sprintId)
            .OrderBy(st => st.Deadline)
            .Select(st => new SprintTaskDetail(
                st.Id, st.Name, st.Description,
                st.Deadline, st.DurationMins, st.CalendarEventId, st.Status
            ))
            .ToListAsync();

        return Ok(tasks);
    }

}
