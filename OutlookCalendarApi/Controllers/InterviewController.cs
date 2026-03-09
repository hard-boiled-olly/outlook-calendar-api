using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Models.Domain;
using OutlookCalendarApi.Models.Dto;

namespace OutlookCalendarApi.Controllers;

[ApiController]
[Route("api/interviews")]
[Authorize]
public class InterviewController(AppDbContext db) : ControllerBase
{
    // POST /api/interviews — create draft identity + interview session
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

        var session = new InterviewSession
        {
            Type = request.Type,
            IdentityId = identity.Id,
            UserId = userId,
            CurrentStep = 0,
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
            "What area of your life do you want to work on?"
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
            session.Type,
            session.CurrentStep,
            session.ConversationHistory,
            session.AccumulatedData,
            session.Active,
            session.ExpiresAt
        ));
    }

    // POST /api/interviews/{id}/respond — stub; Plan 02 adds Claude logic
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

        session.CurrentStep++;
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new InterviewStepResponse(
            session.Id,
            session.IdentityId ?? Guid.Empty,
            session.CurrentStep,
            "[Interview logic coming in Plan 02]"
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
