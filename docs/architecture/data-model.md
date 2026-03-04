---
title: "Outlook Calendar API — Data Model"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Data Model

## Entity Relationship Diagram

```{.mermaid}
erDiagram
    users ||--o{ identities : has
    identities ||--o{ summits : has
    identities ||--o{ habits : has
    summits ||--o{ milestones : has
    milestones ||--o{ sprints : has
    sprints ||--o{ sprint_tasks : has
    sprints ||--o{ habit_events : has
    sprints ||--o{ habit_prescriptions : has
    habits ||--o{ habit_prescriptions : has
    habit_prescriptions ||--o{ habit_events : has

    users {
        uuid id PK
        string display_name
        string email
        timestamp created_at
    }
    identities {
        uuid id PK
        uuid user_id FK
        string area_of_life
        string statement
        string status
        timestamp created_at
        timestamp abandoned_at
    }
    summits {
        uuid id PK
        uuid identity_id FK
        string description
        string proof_criteria
        date target_date
        string status
        timestamp created_at
    }
    milestones {
        uuid id PK
        uuid summit_id FK
        string description
        string proof_criteria
        date target_date
        int sort_order
        string status
        timestamp proved_at
        timestamp created_at
    }
    sprints {
        uuid id PK
        uuid milestone_id FK
        int sprint_number
        timestamp started_at
        timestamp ended_at
        string reflection
        string status
    }
    habits {
        uuid id PK
        uuid identity_id FK
        string name
        string frequency
        timestamp created_at
    }
    habit_prescriptions {
        uuid id PK
        uuid habit_id FK
        uuid sprint_id FK
        string prescription
        timestamp created_at
    }
    habit_events {
        uuid id PK
        uuid habit_prescription_id FK
        uuid sprint_id FK
        date scheduled_date
        time scheduled_time
        int duration_mins
        string calendar_event_id
        string status
    }
    sprint_tasks {
        uuid id PK
        uuid sprint_id FK
        string name
        string description
        date deadline
        int duration_mins
        string calendar_event_id
        string status
    }
```

## Entity Status Values

| Entity | Statuses |
|---|---|
| **Identity** | `active`, `abandoned` |
| **Summit** | `active`, `achieved`, `abandoned` |
| **Milestone** | `pending`, `active`, `proved`, `missed` |
| **Sprint** | `active`, `completed`, `replanned` |
| **HabitEvent** | `pending`, `synced`, `completed`, `skipped` |
| **SprintTask** | `pending`, `synced`, `completed` |

## Design Notes

- **`habit_prescriptions`** is a bridge between `habits` and `sprints` — the same habit persists across sprints but its "dose" (prescription) changes each sprint via replanning
- **`calendar_event_id`** on `habit_events` and `sprint_tasks` links back to Microsoft Graph for update/delete operations
- **`users.id`** is the Azure AD Object ID (not auto-generated) — all other primary keys use `gen_random_uuid()`
- All tables use **snake_case** naming, configured explicitly in `AppDbContext.OnModelCreating`
- **`milestones.sort_order`** determines the progression sequence — when a milestone is proved, the next by sort order is activated
