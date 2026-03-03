using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OutlookCalendarApi.Data;

namespace OutlookCalendarApi.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController(AppDbContext db) : ControllerBase
{
    [HttpPost("me")]
    public async Task<IActionResult> Me()
    {
        if (HttpContext.Items["UserId"] is not Guid userId)
            return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized();

        return Ok(new { user.Id, user.DisplayName, user.Email });
    }
}
