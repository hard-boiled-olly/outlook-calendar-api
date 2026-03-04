---
title: "Flow — Identity Creation"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Identity Creation Flow

A two-step process: the user provides a rough idea of who they want to become, Claude refines it into a powerful identity statement, and the user confirms.

```{.mermaid}
sequenceDiagram
    participant U as User (Desktop App)
    participant API as Outlook Calendar API
    participant Claude as Claude AI
    participant DB as PostgreSQL

    U->>API: POST /api/identities/refine {areaOfLife, roughStatement}
    API->>Claude: RefineIdentityAsync (structured output)
    Claude-->>API: {refinedStatement, explanation}
    API-->>U: {refinedStatement, explanation}

    Note over U: User reviews and confirms

    U->>API: POST /api/identities {areaOfLife, statement}
    API->>DB: INSERT identity (status=active)
    DB-->>API: identity created
    API-->>U: 201 Created {id}
```

## Step-by-step

1. **Refine** — The user sends their area of life (e.g. "fitness") and a rough statement (e.g. "I want to get stronger"). The API passes this to `ClaudeService.RefineIdentityAsync` which uses a coaching system prompt to reframe it as an identity statement like "I'm becoming someone who trains consistently and pushes their physical limits."

2. **Review** — The frontend displays the refined statement and explanation. The user can edit or accept it.

3. **Confirm** — The user submits the final statement. The API creates an `Identity` record with `status=active`. This identity becomes the root of all downstream goals, milestones, and sprints.
