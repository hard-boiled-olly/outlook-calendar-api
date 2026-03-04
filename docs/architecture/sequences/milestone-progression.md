---
title: "Flow — Milestone Progression"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Milestone Progression Flow

When a user proves they've achieved a milestone, the system advances to the next one. If all milestones are proved, the summit is achieved.

```{.mermaid}
sequenceDiagram
    participant U as User
    participant API as Outlook Calendar API
    participant DB as PostgreSQL

    U->>API: POST /api/milestones/{id}/prove
    API->>DB: Mark milestone status=proved, provedAt=now
    API->>DB: End active sprint (status=completed)
    API->>DB: Activate next milestone (status=active)
    alt No more milestones
        API->>DB: Mark summit status=achieved
    end
    API-->>U: {nextMilestoneId, summitAchieved}
```

## Step-by-step

1. **Prove** — The user declares they've achieved the milestone's proof criteria. The API sets `status=proved` and `provedAt=now`.

2. **End sprint** — Any active sprint on this milestone is ended (`status=completed`, `endedAt=now`).

3. **Activate next** — The next milestone by `sort_order` with `status=pending` is activated (`status=active`).

4. **Summit check** — If no more milestones remain, the summit itself is marked `status=achieved`.

5. **Response** — The API returns the next milestone's ID and details, or flags `summitAchieved=true` if the user has reached the summit.

## Milestone State Machine

```{.mermaid}
stateDiagram-v2
    [*] --> pending
    pending --> active : Previous milestone proved
    active --> proved : User proves milestone
    active --> missed : User misses milestone (with reflection)
    missed --> active : After replanning
    proved --> [*]
```
