---
title: "Outlook Calendar API — Architecture Overview"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Outlook Calendar API — Architecture Overview

> Backend API for the ProveIt goal achievement platform — identity-based goal management with AI coaching and Outlook calendar sync.

## System Context (C4 Level 1)

The Outlook Calendar API sits at the centre of the ProveIt platform. It receives requests from the desktop app, delegates AI coaching to Claude, syncs scheduled activities to Outlook via Microsoft Graph, authenticates users via Microsoft Entra ID, and persists all data in PostgreSQL.

```{.mermaid}
C4Context
    Person(user, "ProveIt User", "Sets identity goals, tracks progress via desktop app")
    System(api, "Outlook Calendar API", "ASP.NET Core API — goal management, AI coaching, calendar sync")
    System_Ext(claude, "Claude API", "Anthropic LLM — refines goals, generates plans")
    System_Ext(graph, "Microsoft Graph API", "Creates/deletes Outlook calendar events")
    System_Ext(entra, "Microsoft Entra ID", "OAuth2/JWT authentication")
    System_Ext(postgres, "PostgreSQL", "Stores users, identities, summits, milestones, sprints")
    System_Ext(proveit, "ProveIt Desktop App", "Electron/Tauri frontend")

    Rel(proveit, api, "REST API calls", "HTTPS + JWT")
    Rel(api, claude, "Structured output requests", "HTTPS")
    Rel(api, graph, "Calendar event CRUD", "HTTPS + delegated token")
    Rel(api, entra, "Token validation", "OIDC")
    Rel(api, postgres, "Entity Framework Core", "TCP")
    Rel(user, proveit, "Uses")
```

## Container Architecture (C4 Level 2)

The API is a single ASP.NET Core process backed by PostgreSQL. External integrations are Claude (AI coaching) and Microsoft Graph (calendar sync). Authentication is handled by Microsoft Entra ID via JWT bearer tokens validated by the Microsoft.Identity.Web library.

```{.mermaid}
C4Container
    Person(user, "ProveIt User")

    Container_Boundary(api_boundary, "Outlook Calendar API") {
        Container(webapi, "ASP.NET Core Web API", ".NET 10", "REST endpoints for goal management")
        ContainerDb(db, "PostgreSQL", "Npgsql + EF Core", "Users, identities, summits, milestones, sprints, habits")
    }

    System_Ext(claude, "Claude API (Haiku 4.5)")
    System_Ext(graph, "Microsoft Graph API")
    System_Ext(entra, "Microsoft Entra ID")

    Rel(user, webapi, "HTTPS + JWT Bearer")
    Rel(webapi, db, "EF Core queries")
    Rel(webapi, claude, "Anthropic SDK")
    Rel(webapi, graph, "Microsoft.Graph SDK")
    Rel(webapi, entra, "Token validation")
```

## Key Architectural Decisions

- **Identity-based goal model** — the domain hierarchy (User → Identity → Summit → Milestone → Sprint) embeds James Clear-style identity change into the data model
- **AI-in-the-loop** — Claude is used for 5 distinct coaching functions (identity refinement, summit refinement, milestone generation, sprint planning, replanning) via structured JSON output
- **Delegated calendar access** — the frontend obtains a Graph token and passes it via `X-Graph-Token` header, keeping the API stateless with respect to Graph auth
- **Aspire integration** — uses .NET Aspire's `AddServiceDefaults()` and `AddNpgsqlDbContext()` for service discovery, health checks, and OpenTelemetry
- **Automatic user provisioning** — `UserSyncMiddleware` upserts users on every authenticated request using the Azure AD Object ID as the primary key
