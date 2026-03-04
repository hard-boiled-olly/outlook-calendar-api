---
title: "Outlook Calendar API — Code Walkthrough"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Code Walkthrough

## Directory Structure

```
OutlookCalendarApi/
├── Controllers/
│   ├── AuthController.cs          # User authentication
│   ├── IdentityController.cs      # Identity CRUD + refinement
│   ├── MilestoneController.cs     # Milestone generation + state management
│   ├── PlanController.cs          # Sprint planning + calendar sync
│   └── SummitController.cs        # Summit refinement + creation
├── Data/
│   └── AppDbContext.cs            # EF Core context with 9 DbSets
├── Middleware/
│   └── UserSyncMiddleware.cs      # JWT → User upsert
├── Migrations/
│   └── 20260303193140_InitialCreate.*
├── Models/
│   ├── Claude/                    # Deserialization targets for Claude responses
│   │   ├── IdentityRefinementResponse.cs
│   │   ├── MilestoneGenerationResponse.cs
│   │   ├── ReplanResponse.cs
│   │   ├── SprintPlanResponse.cs
│   │   └── SummitRefinementResponse.cs
│   ├── Domain/                    # EF Core entity classes
│   │   ├── User.cs, Identity.cs, Summit.cs
│   │   ├── Milestone.cs, Sprint.cs, SprintTask.cs
│   │   └── Habit.cs, HabitPrescription.cs, HabitEvent.cs
│   └── Dto/                       # API request/response records
│       ├── IdentityDtos.cs, SummitDtos.cs
│       ├── MilestoneDtos.cs, SprintDtos.cs
│       └── ProveDtos.cs
├── Properties/
│   └── launchSettings.json
├── Services/
│   ├── ClaudeService.cs           # Anthropic SDK wrapper (5 coaching methods)
│   └── GraphCalendarService.cs    # Microsoft Graph calendar operations
├── Program.cs                     # App startup & DI configuration
├── appsettings.json               # Azure AD config
└── OutlookCalendarApi.csproj      # .NET 10, key dependencies
```

## Key Entry Points

### Application Startup

`Program.cs:1-38` — Minimal hosting with .NET Aspire integration:

```csharp
// Program.cs:6-16
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                          // Aspire: health checks, OpenTelemetry
builder.AddNpgsqlDbContext<AppDbContext>("proveitdb");  // Aspire: PostgreSQL service discovery

builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");

builder.Services.AddSingleton<ClaudeService>();
builder.Services.AddSingleton<GraphCalendarService>();
```

Both services are registered as singletons — `ClaudeService` holds an `AnthropicClient` and `GraphCalendarService` is stateless (creates a new `GraphServiceClient` per request using the delegated token).

### User Sync Middleware

`Middleware/UserSyncMiddleware.cs:10-44` — Automatic user provisioning:

```csharp
// UserSyncMiddleware.cs:12-17
var oid = context.User.FindFirstValue(
    "http://schemas.microsoft.com/identity/claims/objectidentifier")
    ?? context.User.FindFirstValue("oid");

if (oid != null && Guid.TryParse(oid, out var userId))
```

Extracts the Azure AD Object ID from the JWT token's `oid` claim. If no User record exists, one is created with `DisplayName` and `Email` from token claims. The `userId` is stored in `HttpContext.Items["UserId"]` for downstream controllers.

### Claude Integration Pattern

`Services/ClaudeService.cs:28-181` — All 5 methods follow the same pattern:

```csharp
// ClaudeService.cs:30-49 (example: RefineIdentityAsync)
var response = await _client.Messages.Create(new MessageCreateParams
{
    Model = Model.ClaudeHaiku4_5,
    MaxTokens = 1024,
    System = IdentitySystemPrompt,
    Messages = [new()
    {
        Role = Role.User,
        Content = $"Area of life: {areaOfLife}\n\nWhat I want: {roughStatement}",
    }],
    OutputConfig = new OutputConfig
    {
        Format = new JsonOutputFormat
        {
            Schema = ParseSchema(IdentitySchema),
        },
    },
});

return JsonSerializer.Deserialize<IdentityRefinementResponse>(ExtractText(response))!;
```

Key design choices:
- **Structured JSON output** via `OutputConfig.Format` ensures responses match the expected schema
- **System prompts** (`ClaudeService.cs:185-288`) encode coaching philosophy and examples
- **Haiku 4.5** is used for all calls — fast and cost-effective for structured coaching responses

### Calendar Sync

`Services/GraphCalendarService.cs:56-106` — Creates Outlook events with a callback pattern:

```csharp
// GraphCalendarService.cs:66-79
foreach (var (habitEventId, date, durationMins) in occurrences)
{
    var startTime = new TimeOnly(9, 0);
    var subject = $"[ProveIt] {habitName}";
    var body = $"{prescription}\n\nIdentity: {identityStatement}";

    var eventId = await CreateEventAsync(
        graphToken, subject, body, date, startTime, durationMins, timeZone);

    await onEventCreated(habitEventId, eventId);
    count++;

    await Task.Delay(250); // Respect Graph rate limit (4 req/sec/mailbox)
}
```

The `onEventCreated` callback updates the database with the Graph event ID as each event is created, ensuring the mapping is persisted even if the process fails partway through.

### Sprint Plan Confirmation

`Controllers/PlanController.cs:62-215` — The most complex endpoint orchestrates:

1. Sprint creation with incrementing `sprint_number`
2. Habit upsert (reuse existing habits by name within an identity)
3. `HabitPrescription` creation (bridge between habit and sprint)
4. `HabitEvent` scheduling via `DistributeEventsAcrossWeeks` (`PlanController.cs:360-385`)
5. `SprintTask` creation
6. Calendar event creation for all habit events and tasks

### Event Distribution Algorithm

`Controllers/PlanController.cs:360-385` — Spreads habit events evenly across weeks:

```csharp
// PlanController.cs:360-385
private static List<DateOnly> DistributeEventsAcrossWeeks(
    DateOnly start, DateOnly end, int perWeek)
{
    var dates = new List<DateOnly>();
    var current = start;

    while (current <= end)
    {
        var weekEnd = current.AddDays(6);
        if (weekEnd > end) weekEnd = end;

        var daysInWeek = weekEnd.DayNumber - current.DayNumber + 1;
        var spacing = Math.Max(1, daysInWeek / perWeek);

        for (int i = 0; i < perWeek; i++)
        {
            var date = current.AddDays(i * spacing);
            if (date > weekEnd) break;
            dates.Add(date);
        }

        current = current.AddDays(7);
    }

    return dates;
}
```

For a "3x per week" habit across a 4-week sprint, this produces 12 evenly-spaced dates. The frequency string is parsed by `ParseFrequency` (`PlanController.cs:351-358`) which handles patterns like "daily", "3x per week", "2 times/week".
