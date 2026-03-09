using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Models.Domain;
using OutlookCalendarApi.Models.Dto;

namespace OutlookCalendarApi.Controllers;

[ApiController]
[Route("api/preferences")]
[Authorize]
public class PreferencesController(AppDbContext db) : ControllerBase
{
    [HttpGet("scheduling")]
    public async Task<IActionResult> GetScheduling()
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var prefs = await db.SchedulingPreferences
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (prefs == null)
        {
            return Ok(new SchedulingPreferencesResponse(
                Guid.Empty,
                "09:00",
                "17:30",
                new Dictionary<string, string>(),
                "Europe/London"
            ));
        }

        return Ok(new SchedulingPreferencesResponse(
            prefs.Id,
            prefs.WorkingHoursStart.ToString("HH:mm"),
            prefs.WorkingHoursEnd.ToString("HH:mm"),
            prefs.PreferredTimes,
            prefs.TimeZone
        ));
    }

    [HttpPut("scheduling")]
    public async Task<IActionResult> UpdateScheduling([FromBody] UpdateSchedulingPreferencesRequest request)
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var prefs = await db.SchedulingPreferences
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (prefs == null)
        {
            prefs = new SchedulingPreferences
            {
                UserId = userId,
                TimeZone = request.TimeZone ?? "Europe/London"
            };
            db.SchedulingPreferences.Add(prefs);
        }

        if (request.WorkingHoursStart is not null && TimeOnly.TryParse(request.WorkingHoursStart, out var start))
            prefs.WorkingHoursStart = start;

        if (request.WorkingHoursEnd is not null && TimeOnly.TryParse(request.WorkingHoursEnd, out var end))
            prefs.WorkingHoursEnd = end;

        if (request.PreferredTimes is not null)
            prefs.PreferredTimes = request.PreferredTimes;

        if (request.TimeZone is not null)
            prefs.TimeZone = request.TimeZone;

        prefs.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new SchedulingPreferencesResponse(
            prefs.Id,
            prefs.WorkingHoursStart.ToString("HH:mm"),
            prefs.WorkingHoursEnd.ToString("HH:mm"),
            prefs.PreferredTimes,
            prefs.TimeZone
        ));
    }
}
