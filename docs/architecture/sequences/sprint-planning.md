---
title: "Flow — Sprint Planning & Calendar Sync"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Sprint Planning & Calendar Sync Flow

The most complex flow in the system. Claude generates a sprint plan (habits + tasks), the user confirms, and the API creates all entities and syncs them to Outlook calendar.

```{.mermaid}
sequenceDiagram
    participant U as User (Desktop App)
    participant API as Outlook Calendar API
    participant Claude as Claude AI
    participant DB as PostgreSQL
    participant Graph as Microsoft Graph

    U->>API: POST /api/milestones/{id}/plan
    API->>DB: Load milestone + summit + identity
    API->>Claude: GenerateSprintPlanAsync
    Claude-->>API: {habits[], tasks[]}
    API-->>U: Generated plan for review

    Note over U: User reviews, edits, confirms

    U->>API: POST /api/milestones/{id}/plan/confirm<br/>(X-Graph-Token header)
    API->>DB: Create Sprint, Habits, HabitPrescriptions, HabitEvents, SprintTasks

    loop Each habit event
        API->>Graph: POST /me/events
        Graph-->>API: calendar event ID
        API->>DB: Update HabitEvent.CalendarEventId
    end

    loop Each sprint task
        API->>Graph: POST /me/events
        Graph-->>API: calendar event ID
        API->>DB: Update SprintTask.CalendarEventId
    end

    API-->>U: {sprintId, calendarEventsCreated}
```

## Step-by-step

1. **Generate** — The API loads the milestone context (identity statement, summit goal, milestone description, proof criteria) and any reflection from a previous sprint. Claude generates 2-4 habits with frequencies/prescriptions and 2-5 one-off tasks.

2. **Review** — The frontend displays the plan. The user can adjust habit frequencies, task deadlines, durations, etc.

3. **Confirm** — The user submits the confirmed plan with an `X-Graph-Token` header (delegated Graph access token obtained by the frontend).

4. **Entity creation** — The API creates:
   - A `Sprint` record (incrementing `sprint_number`)
   - `Habit` records (upserted — reused if already exists for this identity)
   - `HabitPrescription` records (linking habit to sprint with specific prescription)
   - `HabitEvent` records (distributed across the sprint period using `DistributeEventsAcrossWeeks`)
   - `SprintTask` records

5. **Calendar sync** — For each habit event and sprint task, the API creates an Outlook calendar event via Microsoft Graph. Events are prefixed with `[ProveIt]` and include the identity statement in the body. Calendar event IDs are saved back to the database. A 250ms delay between requests respects Graph rate limits.
