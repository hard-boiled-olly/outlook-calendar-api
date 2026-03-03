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
public class IdentityController(AppDbContext db, ClaudeService claude) : ControllerBase
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
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new IdentityListItem(
                i.Id,
                i.AreaOfLife,
                i.Statement,
                i.Status,
                i.CreatedAt,
                i.Summits
                    .Where(s => s.Status == "active")
                    .Select(s => new SummitSummary(s.Id, s.Description, s.Status))
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
}
