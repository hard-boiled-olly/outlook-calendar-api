---
title: "Outlook Calendar API — Getting Started"
date: "2026-03-04"
mainfont: DejaVu Sans
monofont: DejaVu Sans Mono
---

# Getting Started

## Prerequisites

- **.NET 10 SDK**
- **PostgreSQL** — or run via .NET Aspire AppHost which provisions it automatically
- **Anthropic API key** — set as `ANTHROPIC_API_KEY` environment variable
- **Azure AD app registration** — tenant/client IDs configured in `appsettings.json`
- **`core` repo** — must be cloned as a sibling directory (`~/core`) for the `Core.ServiceDefaults` project reference

## Setup

```bash
# Clone both repos
git clone <repo-url> ~/outlook-calendar-api
git clone <core-repo-url> ~/core

# Restore dependencies
cd ~/outlook-calendar-api/OutlookCalendarApi
dotnet restore
```

## Database

```bash
# Apply EF Core migrations (requires PostgreSQL connection string)
cd ~/outlook-calendar-api/OutlookCalendarApi
dotnet ef database update
```

When running via Aspire, the connection string is injected automatically via the `proveitdb` service name.

## Running Locally

```bash
# Set required environment variable
export ANTHROPIC_API_KEY=sk-ant-...

# Run the API
cd ~/outlook-calendar-api/OutlookCalendarApi
dotnet run
```

Alternatively, run via the Aspire AppHost (in the `core` repo) for full orchestration with PostgreSQL and service discovery.

## API Documentation

In development mode, OpenAPI docs are available at `/openapi/v1.json`.

## Authentication

All endpoints require a valid Azure AD JWT bearer token. The token must contain an `oid` claim (Azure AD Object ID). Users are automatically provisioned on first request.

For calendar sync endpoints (`POST /api/milestones/{id}/plan/confirm`, `DELETE /api/identities/{id}`), an additional `X-Graph-Token` header is required containing a delegated Microsoft Graph access token with `Calendars.ReadWrite` scope.

## Configuration

Key configuration in `appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-client-id>",
    "Audience": "<your-client-id>"
  }
}
```

The `ANTHROPIC_API_KEY` environment variable is read automatically by the Anthropic SDK.
