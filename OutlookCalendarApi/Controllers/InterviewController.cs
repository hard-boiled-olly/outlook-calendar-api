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

        var result = await interviews.ProcessResponseAsync(session.ConversationHistory, request.Answer);

        session.ConversationHistory = result.UpdatedHistoryJson;
        session.CurrentStep++;
        session.UpdatedAt = DateTime.UtcNow;

        InterviewSummaryDto? summary = null;

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

        await db.SaveChangesAsync();

        return Ok(new InterviewRespondResponse(
            session.Id,
            result.IsComplete,
            result.NextQuestion,
            summary
        ));
    }

    // POST /api/interviews/{id}/confirm — activate identity, create summit, close session
    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var session = await db.InterviewSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && s.DeletedAt == null);

        if (session == null)
            return NotFound();

        if (session.AccumulatedData == "{}" || string.IsNullOrEmpty(session.AccumulatedData))
            return BadRequest("Interview is not complete yet.");

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
        if (!string.IsNullOrEmpty(output.TargetDate) &&
            DateOnly.TryParse(output.TargetDate, out var parsedDate))
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

        session.Active = false;
        session.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new InterviewConfirmResponse(identity.Id, summit.Id));
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
