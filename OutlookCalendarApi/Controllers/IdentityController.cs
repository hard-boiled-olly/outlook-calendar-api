using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Models.Domain;
using OutlookCalendarApi.Models.Dto;
using OutlookCalendarApi.Services;

namespace OutlookCalendarApi.Controllers;

[ApiController]
[Route("api/identities")]
[Authorize]
public class IdentityController(AppDbContext db, ClaudeService claude, GraphCalendarService graph) : ControllerBase
{
    [HttpPost("refine")]
    public async Task<IActionResult> Refine([FromBody] CreateIdentityRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid)
            return Unauthorized();

        var result = await claude.RefineIdentityAsync(request.AreaOfLife, request.RoughStatement);
        return Ok(new IdentityRefinementResult(result.RefinedStatement, result.Explanation));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ConfirmIdentityRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var identity = new Identity
        {
            UserId = userId,
            AreaOfLife = request.AreaOfLife,
            Statement = request.Statement,
            Status = "active"
        };

        db.Identities.Add(identity);
        await db.SaveChangesAsync();

        return Created($"/api/identities/{identity.Id}", new { identity.Id });
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var identities = await db.Identities
            .Where(i => i.UserId == userId && i.Status == "active")
            .Include(i => i.Summits)
                .ThenInclude(s => s.Milestones)
                    .ThenInclude(m => m.Sprints)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new IdentityListItem(
                i.Id,
                i.AreaOfLife,
                i.Statement,
                i.Status,
                i.CreatedAt,
                i.Summits
                    .Where(s => s.Status == "active")
                    .Select(s => new SummitSummary(
                        s.Id, s.Description, s.Status,
                        s.Milestones
                            .Where(m => m.Status == "active")
                            .OrderBy(m => m.SortOrder)
                            .Select(m => new ActiveMilestoneSummary(
                                m.Id, m.Description, m.TargetDate, m.SortOrder, m.Status,
                                m.Sprints
                                    .Where(sp => sp.Status == "active")
                                    .Select(sp => new ActiveSprintSummary(sp.Id, sp.SprintNumber))
                                    .FirstOrDefault()
                            ))
                            .FirstOrDefault()
                    ))
                    .FirstOrDefault()
            ))
            .ToListAsync();

        return Ok(identities);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var identity = await db.Identities
            .Where(i => i.Id == id && i.UserId == userId)
            .Include(i => i.Summits)
            .FirstOrDefaultAsync();

        if (identity == null)
            return NotFound();

        return Ok(new IdentityDetail(
            identity.Id,
            identity.AreaOfLife,
            identity.Statement,
            identity.Status,
            identity.CreatedAt,
            identity.Summits.Select(s => new SummitDetail(
                s.Id, s.Description, s.ProofCriteria,
                s.TargetDate, s.Status, s.CreatedAt
            )).ToList()
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> GiveUp(Guid id)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var graphToken = Request.Headers["X-Graph-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(graphToken))
            return BadRequest("X-Graph-Token header required for calendar cleanup");

        var identity = await db.Identities
            .Where(i => i.Id == id && i.UserId == userId)
            .Include(i => i.Summits)
                .ThenInclude(s => s.Milestones)
                    .ThenInclude(m => m.Sprints)
            .FirstOrDefaultAsync();

        if (identity == null)
            return NotFound();

        // Abandon identity
        identity.Status = "abandoned";
        identity.AbandonedAt = DateTime.UtcNow;

        // Abandon all summits and end all active sprints
        foreach (var summit in identity.Summits)
        {
            summit.Status = "abandoned";
            foreach (var milestone in summit.Milestones)
            {
                foreach (var sprint in milestone.Sprints.Where(s => s.Status == "active"))
                {
                    sprint.Status = "completed";
                    sprint.EndedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync();

        // Collect all calendar event IDs for this identity
        var habitEventIds = await db.HabitEvents
            .Where(he => he.HabitPrescription.Habit.IdentityId == id
                      && he.CalendarEventId != null
                      && he.ScheduledDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            .Select(he => he.CalendarEventId!)
            .ToListAsync();

        var taskEventIds = await db.SprintTasks
            .Where(st => st.Sprint.Milestone.Summit.IdentityId == id
                      && st.CalendarEventId != null
                      && st.Deadline >= DateOnly.FromDateTime(DateTime.UtcNow))
            .Select(st => st.CalendarEventId!)
            .ToListAsync();

        var allEventIds = habitEventIds.Concat(taskEventIds).ToList();

        if (allEventIds.Count > 0)
        {
            await graph.DeleteEventsAsync(graphToken, allEventIds);
        }

        return NoContent();
    }
}
