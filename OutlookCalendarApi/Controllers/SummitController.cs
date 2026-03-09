using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Models.Domain;
using OutlookCalendarApi.Models.Dto;

namespace OutlookCalendarApi.Controllers;

[ApiController]
[Authorize]
public class SummitController(AppDbContext db) : ControllerBase
{
    [HttpPost("api/identities/{identityId:guid}/summit")]
    public async Task<IActionResult> Create(Guid identityId, [FromBody] ConfirmSummitRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var identity = await db.Identities
            .FirstOrDefaultAsync(i => i.Id == identityId && i.UserId == userId);

        if (identity == null)
            return NotFound();

        var summit = new Summit
        {
            IdentityId = identityId,
            Description = request.Description,
            ProofCriteria = request.ProofCriteria,
            TargetDate = request.TargetDate,
            Status = "active"
        };

        db.Summits.Add(summit);
        await db.SaveChangesAsync();

        return Created($"/api/summits/{summit.Id}", new { summit.Id });
    }

    [HttpGet("api/summits/{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var summit = await db.Summits
            .Include(s => s.Identity)
            .FirstOrDefaultAsync(s => s.Id == id && s.Identity.UserId == userId);

        if (summit == null)
            return NotFound();

        return Ok(new SummitDetail(
            summit.Id, summit.Description, summit.ProofCriteria,
            summit.TargetDate, summit.Status, summit.CreatedAt
        ));
    }
}
