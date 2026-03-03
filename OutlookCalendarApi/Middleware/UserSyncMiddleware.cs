using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Models.Domain;

namespace OutlookCalendarApi.Middleware;

public class UserSyncMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var oid = context.User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
                      ?? context.User.FindFirstValue("oid");

            if (oid != null && Guid.TryParse(oid, out var userId))
            {
                var user = await db.Users.FindAsync(userId);
                if (user == null)
                {
                    var displayName = context.User.FindFirstValue("name")
                                      ?? context.User.FindFirstValue(ClaimTypes.Name)
                                      ?? "Unknown";
                    var email = context.User.FindFirstValue("preferred_username")
                                ?? context.User.FindFirstValue(ClaimTypes.Email)
                                ?? "unknown@unknown.com";

                    user = new User
                    {
                        Id = userId,
                        DisplayName = displayName,
                        Email = email
                    };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }

                context.Items["UserId"] = userId;
            }
        }

        await next(context);
    }
}
