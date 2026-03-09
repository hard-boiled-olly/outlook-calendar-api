using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using OutlookCalendarApi.Data;
using OutlookCalendarApi.Middleware;
using OutlookCalendarApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("proveitdb");

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");

builder.Services.AddSingleton<ClaudeService>();
builder.Services.AddSingleton<InterviewService>();
builder.Services.AddSingleton<GraphCalendarService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserSyncMiddleware>();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
