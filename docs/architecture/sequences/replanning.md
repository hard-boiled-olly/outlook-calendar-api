---
title: "Flow — Replanning After Sprint"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Replanning Flow

After proving a milestone or missing one, the user reflects on what worked and what didn't. Claude uses this reflection to adjust habit prescriptions and suggest new tasks for the next sprint.

```{.mermaid}
sequenceDiagram
    participant U as User
    participant API as Outlook Calendar API
    participant Claude as Claude AI
    participant DB as PostgreSQL

    U->>API: POST /api/sprints/{id}/replan {reflection}
    API->>DB: Save reflection on sprint
    API->>DB: Load current habits + prescriptions
    API->>Claude: ReplanAsync (with reflection + current habits)
    Claude-->>API: {updatedPrescriptions, newTasks, coachingNote}
    API->>API: Merge updated prescriptions with existing habits
    API-->>U: {coachingNote, mergedHabits, newTasks}

    Note over U: User confirms → calls POST /plan/confirm to start new sprint
```

## Step-by-step

1. **Reflect** — The user submits a reflection on the completed sprint (what worked, what didn't, what they learned).

2. **Load context** — The API loads the current habits and their prescriptions from the completed sprint, formatting them as a description string for Claude.

3. **Replan** — Claude receives the full context: identity, summit, completed milestone, next milestone, current habits, and reflection. It returns:
   - **Updated prescriptions** — increased "doses" for existing habits (matched by `habit_id`)
   - **New tasks** — one-off actions for the next milestone
   - **Coaching note** — motivational message acknowledging progress

4. **Merge** — The API merges Claude's updated prescriptions with the existing habit list. Habits that Claude didn't mention keep their current prescription.

5. **Review & confirm** — The frontend displays the merged plan and coaching note. The user confirms, which triggers the standard plan confirmation flow (creating a new sprint with calendar sync).
