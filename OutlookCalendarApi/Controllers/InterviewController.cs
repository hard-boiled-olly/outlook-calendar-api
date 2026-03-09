using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Models.Claude;
using OutlookCalendarApi.Models.Domain;
using OutlookCalendarApi.Models.Dto;
using OutlookCalendarApi.Services;

namespace OutlookCalendarApi.Controllers;

[ApiController]
[Route("api/interviews")]
[Authorize]
public class InterviewController(AppDbContext db, InterviewService interviews) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // POST /api/interviews — create draft identity + interview session, get first question
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartInterviewRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var identity = new Identity
        {
            UserId = userId,
            Statement = "",
            Active = false
        };
        db.Identities.Add(identity);

        var (firstQuestion, historyJson) = await interviews.StartAsync();

        var session = new InterviewSession
        {
            Type = request.Type,
            IdentityId = identity.Id,
            UserId = userId,
            CurrentStep = 1,
            ConversationHistory = historyJson,
            Active = true,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        db.InterviewSessions.Add(session);

        await db.SaveChangesAsync();

        return Created($"/api/interviews/{session.Id}", new InterviewStepResponse(
            session.Id,
            identity.Id,
            session.CurrentStep,
            firstQuestion
        ));
    }

    // POST /api/interviews/milestone-sprint — start milestone-sprint interview for a confirmed identity+summit
    [HttpPost("milestone-sprint")]
    public async Task<IActionResult> StartMilestoneSprint([FromBody] StartMilestoneSprintRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var identity = await db.Identities
            .FirstOrDefaultAsync(i => i.Id == request.IdentityId && i.UserId == userId && i.DeletedAt == null);

        if (identity == null)
            return NotFound("Identity not found.");

        var summit = await db.Summits
            .FirstOrDefaultAsync(s => s.Id == request.SummitId && s.IdentityId == identity.Id);

        if (summit == null)
            return NotFound("Summit not found.");

        var (firstQuestion, historyJson) = await interviews.StartMilestoneSprintAsync(
            identity.Statement, summit.Description, summit.ProofCriteria,
            summit.TargetDate?.ToString("yyyy-MM-dd"));

        var session = new InterviewSession
        {
            Type = "milestone-sprint",
            IdentityId = identity.Id,
            SummitId = summit.Id,
            UserId = userId,
            CurrentStep = 1,
            ConversationHistory = historyJson,
            Active = true,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        db.InterviewSessions.Add(session);

        await db.SaveChangesAsync();

        return Created($"/api/interviews/{session.Id}", new InterviewStepResponse(
            session.Id,
            identity.Id,
            session.CurrentStep,
            firstQuestion
        ));
    }

    // GET /api/interviews/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var session = await db.InterviewSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && s.DeletedAt == null);

        if (session == null)
            return NotFound();

        if (session.ExpiresAt < DateTime.UtcNow)
            return Gone();

        return Ok(new InterviewStateResponse(
            session.Id,
            session.IdentityId,
            session.SummitId,
            session.Type,
            session.CurrentStep,
            session.ConversationHistory,
            session.AccumulatedData,
            session.Active,
            session.ExpiresAt
        ));
    }

    // POST /api/interviews/{id}/respond
    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] InterviewRespondRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var session = await db.InterviewSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && s.DeletedAt == null);

        if (session == null)
            return NotFound();

        if (session.ExpiresAt < DateTime.UtcNow)
            return Gone();

        var result = await interviews.ProcessResponseAsync(session.Type, session.ConversationHistory, request.Answer);

        session.ConversationHistory = result.UpdatedHistoryJson;
        session.CurrentStep++;
        session.UpdatedAt = DateTime.UtcNow;

        InterviewSummaryDto? summary = null;
        MilestoneSprintSummaryDto? msSummary = null;

        if (result.IsComplete && result.Output is not null)
        {
            session.AccumulatedData = JsonSerializer.Serialize(result.Output, JsonOptions);

            summary = new InterviewSummaryDto(
                result.Output.IdentityStatement,
                result.Output.SummitDescription,
                result.Output.ProofCriteria,
                result.Output.TargetDate,
                result.Output.SummaryBreakdown
                    .Select(b => new SummaryBreakdownDto(b.Component, b.BasedOn))
                    .ToArray()
            );
        }
        else if (result.IsComplete && result.MilestoneSprintOutput is not null)
        {
            session.AccumulatedData = JsonSerializer.Serialize(result.MilestoneSprintOutput, JsonOptions);

            msSummary = new MilestoneSprintSummaryDto(
                result.MilestoneSprintOutput.Milestones
                    .Select(m => new MilestoneSummaryItem(m.Description, m.ProofCriteria, m.TargetDate, m.SortOrder))
                    .ToArray(),
                result.MilestoneSprintOutput.FirstSprintHabits
                    .Select(h => new HabitSummaryItem(h.Name, h.Frequency, h.Prescription, h.DurationMins))
                    .ToArray(),
                result.MilestoneSprintOutput.FirstSprintTasks
                    .Select(t => new TaskSummaryItem(t.Name, t.Description, t.Deadline, t.DurationMins))
                    .ToArray(),
                result.MilestoneSprintOutput.PlanBreakdown
                    .Select(b => new SummaryBreakdownDto(b.Component, b.BasedOn))
                    .ToArray()
            );
        }

        await db.SaveChangesAsync();

        return Ok(new InterviewRespondResponse(
            session.Id,
            result.IsComplete,
            result.NextQuestion,
            summary,
            msSummary
        ));
    }

    // POST /api/interviews/{id}/confirm — activate identity + create summit, or create milestones + sprint
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmInterviewRequest? request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var session = await db.InterviewSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && s.DeletedAt == null);

        if (session == null)
            return NotFound();

        if (session.AccumulatedData == "{}" || string.IsNullOrEmpty(session.AccumulatedData))
            return BadRequest("Interview is not complete yet.");

        session.Active = false;
        session.UpdatedAt = DateTime.UtcNow;

        if (session.Type == "milestone-sprint")
            return await ConfirmMilestoneSprint(session);

        return await ConfirmIdentitySummit(session, request, userId);
    }

    private async Task<IActionResult> ConfirmIdentitySummit(
        InterviewSession session, ConfirmInterviewRequest? request, Guid userId)
    {
        if (!session.IdentityId.HasValue)
            return BadRequest("Session has no associated identity.");

        var output = JsonSerializer.Deserialize<InterviewOutput>(session.AccumulatedData, JsonOptions)!;

        var identity = await db.Identities
            .FirstOrDefaultAsync(i => i.Id == session.IdentityId.Value && i.UserId == userId);

        if (identity == null)
            return NotFound();

        identity.Statement = output.IdentityStatement;
        identity.Active = true;

        DateOnly? targetDate = null;
        var dateSource = request?.TargetDate ?? output.TargetDate;
        if (!string.IsNullOrEmpty(dateSource) &&
            DateOnly.TryParse(dateSource, out var parsedDate))
        {
            targetDate = parsedDate;
        }

        var summit = new Summit
        {
            IdentityId = identity.Id,
            Description = output.SummitDescription,
            ProofCriteria = output.ProofCriteria,
            TargetDate = targetDate,
            Status = "active"
        };
        db.Summits.Add(summit);

        await db.SaveChangesAsync();

        return Ok(new InterviewConfirmResponse(identity.Id, summit.Id));
    }

    private async Task<IActionResult> ConfirmMilestoneSprint(InterviewSession session)
    {
        if (!session.SummitId.HasValue || !session.IdentityId.HasValue)
            return BadRequest("Session has no associated summit or identity.");

        var output = JsonSerializer.Deserialize<MilestoneSprintOutput>(session.AccumulatedData, JsonOptions)!;

        // Create milestones — first one is "active", rest are "pending"
        var milestones = output.Milestones
            .OrderBy(m => m.SortOrder)
            .Select((m, index) => new Milestone
            {
                SummitId = session.SummitId.Value,
                Description = m.Description,
                ProofCriteria = m.ProofCriteria,
                TargetDate = DateOnly.Parse(m.TargetDate),
                SortOrder = m.SortOrder,
                Status = index == 0 ? "active" : "pending"
            })
            .ToList();

        db.Milestones.AddRange(milestones);

        // Create sprint for the first milestone
        var firstMilestone = milestones[0];
        var sprint = new Sprint
        {
            MilestoneId = firstMilestone.Id,
            SprintNumber = 1,
            StartedAt = DateTime.UtcNow,
            Status = "active"
        };
        db.Sprints.Add(sprint);

        // Create habits, prescriptions, and tasks
        foreach (var habitItem in output.FirstSprintHabits)
        {
            var habit = new Habit
            {
                IdentityId = session.IdentityId.Value,
                Name = habitItem.Name,
                Frequency = habitItem.Frequency
            };
            db.Habits.Add(habit);

            var prescription = new HabitPrescription
            {
                HabitId = habit.Id,
                SprintId = sprint.Id,
                Prescription = habitItem.Prescription
            };
            db.HabitPrescriptions.Add(prescription);
        }

        foreach (var taskItem in output.FirstSprintTasks)
        {
            var task = new SprintTask
            {
                SprintId = sprint.Id,
                Name = taskItem.Name,
                Description = taskItem.Description,
                Deadline = DateOnly.Parse(taskItem.Deadline),
                DurationMins = taskItem.DurationMins,
                Status = "pending"
            };
            db.SprintTasks.Add(task);
        }

        // Save scheduling preferences if extracted
        if (output.SchedulingPreferences is not null)
        {
            var prefs = await db.SchedulingPreferences
                .FirstOrDefaultAsync(sp => sp.UserId == session.UserId);

            if (prefs == null)
            {
                prefs = new SchedulingPreferences
                {
                    UserId = session.UserId,
                    TimeZone = "Europe/London"
                };
                db.SchedulingPreferences.Add(prefs);
            }

            if (output.SchedulingPreferences.WorkingHoursStart is not null
                && TimeOnly.TryParse(output.SchedulingPreferences.WorkingHoursStart, out var start))
                prefs.WorkingHoursStart = start;

            if (output.SchedulingPreferences.WorkingHoursEnd is not null
                && TimeOnly.TryParse(output.SchedulingPreferences.WorkingHoursEnd, out var end))
                prefs.WorkingHoursEnd = end;

            if (output.SchedulingPreferences.PreferredTimes is not null)
                prefs.PreferredTimes = output.SchedulingPreferences.PreferredTimes;

            prefs.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new MilestoneSprintConfirmResponse(
            milestones.Select(m => m.Id).ToArray(),
            sprint.Id
        ));
    }

    // DELETE /api/interviews/{id} — abandon session and draft identity
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var session = await db.InterviewSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && s.DeletedAt == null);

        if (session == null)
            return NotFound();

        session.DeletedAt = DateTime.UtcNow;

        if (session.IdentityId.HasValue)
        {
            var identity = await db.Identities
                .FirstOrDefaultAsync(i => i.Id == session.IdentityId.Value && !i.Active);
            if (identity != null)
                identity.DeletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    private StatusCodeResult Gone() => StatusCode(410);
}
