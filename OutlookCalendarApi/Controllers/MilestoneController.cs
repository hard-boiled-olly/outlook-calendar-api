using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Models.Domain;
using OutlookCalendarApi.Models.Dto;

namespace OutlookCalendarApi.Controllers;

[ApiController]
[Authorize]
public class MilestoneController(AppDbContext db) : ControllerBase
{
    [HttpPost("api/summits/{summitId:guid}/milestones/confirm")]
    public async Task<IActionResult> Confirm(Guid summitId, [FromBody] ConfirmMilestonesRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var summit = await db.Summits
            .Include(s => s.Identity)
            .FirstOrDefaultAsync(s => s.Id == summitId && s.Identity.UserId == userId);

        if (summit == null)
            return NotFound();

        if (await db.Milestones.AnyAsync(m => m.SummitId == summitId))
            return Conflict("Milestones already exist for this summit");

        var milestones = request.Milestones.Select((m, i) => new Milestone
        {
            SummitId = summitId,
            Description = m.Description,
            ProofCriteria = m.ProofCriteria,
            TargetDate = m.TargetDate,
            SortOrder = i + 1,
            Status = i == 0 ? "active" : "pending"
        }).ToList();

        db.Milestones.AddRange(milestones);
        await db.SaveChangesAsync();

        return Created($"/api/summits/{summitId}/milestones",
            milestones.Select(m => m.Id).ToList());
    }

    [HttpGet("api/summits/{summitId:guid}/milestones")]
    public async Task<IActionResult> List(Guid summitId)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var summit = await db.Summits
            .Include(s => s.Identity)
            .FirstOrDefaultAsync(s => s.Id == summitId && s.Identity.UserId == userId);

        if (summit == null)
            return NotFound();

        var milestones = await db.Milestones
            .Where(m => m.SummitId == summitId)
            .OrderBy(m => m.SortOrder)
            .Select(m => new MilestoneListItem(
                m.Id, m.Description, m.ProofCriteria,
                m.TargetDate, m.SortOrder, m.Status, m.ProvedAt
            ))
            .ToListAsync();

        return Ok(milestones);
    }

    [HttpPost("api/milestones/{milestoneId:guid}/prove")]
    public async Task<IActionResult> Prove(Guid milestoneId)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var milestone = await db.Milestones
            .Include(m => m.Summit)
            .ThenInclude(s => s.Identity)
            .Include(m => m.Sprints)
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.Summit.Identity.UserId == userId);

        if (milestone == null)
            return NotFound();

        if (milestone.Status != "active")
            return BadRequest("Only active milestones can be proved");

        // Mark milestone as proved
        milestone.Status = "proved";
        milestone.ProvedAt = DateTime.UtcNow;

        // End active sprint
        var activeSprint = milestone.Sprints.FirstOrDefault(s => s.Status == "active");
        if (activeSprint != null)
        {
            activeSprint.Status = "completed";
            activeSprint.EndedAt = DateTime.UtcNow;
        }

        // Find next milestone
        var nextMilestone = await db.Milestones
            .Where(m => m.SummitId == milestone.SummitId
                     && m.SortOrder > milestone.SortOrder
                     && m.Status == "pending")
            .OrderBy(m => m.SortOrder)
            .FirstOrDefaultAsync();

        if (nextMilestone != null)
        {
            nextMilestone.Status = "active";
        }
        else
        {
            // All milestones done — summit achieved
            milestone.Summit.Status = "achieved";
        }

        await db.SaveChangesAsync();

        return Ok(new ProveMilestoneResult(
            nextMilestone?.Id,
            nextMilestone == null,
            nextMilestone?.Description,
            nextMilestone?.ProofCriteria
        ));
    }

    [HttpPost("api/milestones/{milestoneId:guid}/miss")]
    public async Task<IActionResult> Miss(Guid milestoneId, [FromBody] MissMilestoneRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var milestone = await db.Milestones
            .Include(m => m.Summit)
            .ThenInclude(s => s.Identity)
            .Include(m => m.Sprints)
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.Summit.Identity.UserId == userId);

        if (milestone == null)
            return NotFound();

        if (milestone.Status != "active")
            return BadRequest("Only active milestones can be missed");

        // End active sprint with reflection
        var activeSprint = milestone.Sprints.FirstOrDefault(s => s.Status == "active");
        if (activeSprint != null)
        {
            activeSprint.Status = "replanned";
            activeSprint.EndedAt = DateTime.UtcNow;
            activeSprint.Reflection = request.Reflection;
        }

        await db.SaveChangesAsync();

        return Ok(new { milestoneId });
    }

    [HttpPut("api/milestones/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMilestoneRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var milestone = await db.Milestones
            .Include(m => m.Summit)
            .ThenInclude(s => s.Identity)
            .FirstOrDefaultAsync(m => m.Id == id && m.Summit.Identity.UserId == userId);

        if (milestone == null)
            return NotFound();

        if (request.Description != null) milestone.Description = request.Description;
        if (request.ProofCriteria != null) milestone.ProofCriteria = request.ProofCriteria;
        if (request.TargetDate != null) milestone.TargetDate = request.TargetDate.Value;

        await db.SaveChangesAsync();

        return Ok(new MilestoneListItem(
            milestone.Id, milestone.Description, milestone.ProofCriteria,
            milestone.TargetDate, milestone.SortOrder, milestone.Status, milestone.ProvedAt
        ));
    }
}
