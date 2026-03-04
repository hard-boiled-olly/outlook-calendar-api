---
title: "Outlook Calendar API — Components"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Components (C4 Level 3)

## Component Diagram

```{.mermaid}
graph TD
    subgraph Controllers
        AuthCtrl[AuthController]
        IdentCtrl[IdentityController]
        SummitCtrl[SummitController]
        MileCtrl[MilestoneController]
        PlanCtrl[PlanController]
    end

    subgraph Services
        ClaudeSvc[ClaudeService]
        GraphSvc[GraphCalendarService]
    end

    subgraph Middleware
        UserSync[UserSyncMiddleware]
    end

    subgraph Data
        DbCtx[AppDbContext]
    end

    UserSync --> DbCtx
    AuthCtrl --> DbCtx
    IdentCtrl --> DbCtx
    IdentCtrl --> ClaudeSvc
    IdentCtrl --> GraphSvc
    SummitCtrl --> DbCtx
    SummitCtrl --> ClaudeSvc
    MileCtrl --> DbCtx
    MileCtrl --> ClaudeSvc
    PlanCtrl --> DbCtx
    PlanCtrl --> ClaudeSvc
    PlanCtrl --> GraphSvc
```

## Component Details

| Component | Responsibility |
|---|---|
| **UserSyncMiddleware** | Extracts Azure AD OID from JWT, upserts User record, sets `UserId` in `HttpContext.Items` |
| **AuthController** | Single `POST /api/auth/me` endpoint — returns authenticated user profile |
| **IdentityController** | CRUD for identities + Claude-powered refinement. Handles identity abandonment with cascading calendar cleanup |
| **SummitController** | Refine and create summit goals under an identity |
| **MilestoneController** | Generate milestones via Claude, confirm, list, prove, miss, and update milestones. Manages milestone state machine (pending → active → proved) |
| **PlanController** | Sprint planning — generates plans via Claude, confirms with calendar sync via Graph, handles replanning. Also serves sprint habit/task detail endpoints |
| **ClaudeService** | Wraps Anthropic SDK with 5 structured-output methods (Haiku 4.5). Each method has a coaching system prompt and JSON schema |
| **GraphCalendarService** | Creates/deletes Outlook calendar events via Microsoft Graph SDK. Handles rate limiting (250ms delay between requests) |
| **AppDbContext** | EF Core DbContext with 9 entity sets and snake_case column mapping |

## API Endpoints

### AuthController (`api/auth`)
| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/me` | Get authenticated user profile |

### IdentityController (`api/identities`)
| Method | Route | Description |
|---|---|---|
| POST | `/api/identities/refine` | Refine identity statement via Claude |
| POST | `/api/identities` | Create confirmed identity |
| GET | `/api/identities` | List active identities with nested summit/milestone/sprint summaries |
| GET | `/api/identities/{id}` | Get identity detail with all summits |
| DELETE | `/api/identities/{id}` | Abandon identity — cascades to summits/sprints, cleans up calendar events |

### SummitController
| Method | Route | Description |
|---|---|---|
| POST | `/api/identities/{id}/summit/refine` | Refine summit goal via Claude |
| POST | `/api/identities/{id}/summit` | Create confirmed summit |
| GET | `/api/summits/{id}` | Get summit detail |

### MilestoneController
| Method | Route | Description |
|---|---|---|
| POST | `/api/summits/{id}/milestones` | Generate milestones via Claude |
| POST | `/api/summits/{id}/milestones/confirm` | Confirm and persist generated milestones |
| GET | `/api/summits/{id}/milestones` | List milestones for a summit |
| POST | `/api/milestones/{id}/prove` | Mark milestone as proved, activate next |
| POST | `/api/milestones/{id}/miss` | Record missed milestone with reflection |
| PUT | `/api/milestones/{id}` | Update milestone description/criteria/date |

### PlanController
| Method | Route | Description |
|---|---|---|
| POST | `/api/milestones/{id}/plan` | Generate sprint plan via Claude |
| POST | `/api/milestones/{id}/plan/confirm` | Confirm plan — creates sprint, habits, tasks, calendar events |
| POST | `/api/sprints/{id}/replan` | Replan after sprint with reflection |
| GET | `/api/sprints/{id}/habits` | Get sprint habit details with events |
| GET | `/api/sprints/{id}/tasks` | Get sprint task details |
