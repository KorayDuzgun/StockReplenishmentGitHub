# Stock Replenishment Request System

Production-line workers raise material replenishment requests; reviewers approve, reject and fulfil them. Submitted requests trigger a slow external availability check that runs asynchronously without blocking the API.

## Solution Layout

```
src/
  StockReplenishment.Contracts   ← DTOs + Enums (zero NuGet deps)
  StockReplenishment.Data        ← Entities, DbContext, Repositories, Seeder
  StockReplenishment.Services    ← Workflow service, async worker, abstractions
  StockReplenishment.Api         ← REST endpoints, identity, middleware
  StockReplenishment.Web         ← Blazor UI (refs Contracts only)
tests/
  StockReplenishment.Tests       ← Service + worker tests
```

## Run

```bash
# API (http://localhost:5283)
dotnet run --project src/StockReplenishment.Api --launch-profile http

# Web (http://localhost:5156) — needs the API running
dotnet run --project src/StockReplenishment.Web --launch-profile http
```

## Test

```bash
dotnet test
```

## Identity (demo)

The API uses two HTTP headers for the simulated identity:

- `X-User`: any user name (e.g. `johan`, `anna`)
- `X-User-Role`: `Worker` or `Reviewer`

The Web app exposes a user switcher in the top bar that sets these headers automatically.
