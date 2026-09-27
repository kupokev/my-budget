# DD-0001: Architecture overview

> **Status:** Active
> **Last updated:** 2026-09-26
> **Related ADRs:** ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, ADR-0007

## Context / Problem

MyBudget replaces a 2013–2026 spreadsheet budget with a single-user, self-hosted budget app. It
needs a paycheck estimator, an HSA planner, a credit-card rewards optimizer, and lightweight
investment tracking, on top of the usual bills/accounts/cards/debts. It's a personal project, not a
Fact Foundry product, hosted on the user's own home lab, and it should avoid paid integrations
wherever a free or manual alternative exists.

## Design

### Layers

| Layer | Choice | Why |
| --- | --- | --- |
| UI | Razor class library `MyBudget.UI`; Photino.Blazor desktop host on Linux; MAUI Android host later | One shared Razor UI, host-agnostic. MAUI has no Linux desktop target and Kevin only uses Linux, so Photino hosts the desktop (ADR-0004). |
| API | ASP.NET Core minimal APIs | Straightforward, matches the .NET background. |
| Calculation engines | Separate libraries (PaycheckEngine, HsaPlanner, RewardsOptimizer, Amortization) | No UI or DB dependencies; unit-tested against real pay stubs and real HSA/rewards numbers so a wrong number is caught as a failing test, not discovered live. |
| Data | EF Core; in-memory provider in Development, PostgreSQL (existing network server) in Production, chosen by config (ADR-0005) | Dev model can churn without migrations; reference tables for tax brackets and contribution limits are keyed by year so rule changes don't need code changes. |
| Charts | FactFoundry.Blazor.Charts | Existing library, avoid pulling in a second charting dependency. |
| Styling | Hand-written CSS + QuickGrid, no component framework | Lighter and lower maintenance than MudBlazor/Bootstrap (ADR-0007). |
| Auth | LAN-only, single shared API key, HTTPS, no MFA | Single user on a home network; tunnel (cloudflared) if ever used remotely (ADR-0006). Full account numbers are stored (ADR-0008). |
| Import | CSV/OFX per institution | No paid aggregator (see ADR-0001). |
| Local AI | Ollama + Open WebUI (an instance you already run), tool-calling model | Private, free, and avoids the accuracy problems of an LLM writing its own database queries (see ADR-0003). |
| Hosting | `dotnet publish` + systemd on a Linux server on the home network | Self-hosted, no Docker by preference (ADR-0005). |

### Sequencing

Desktop app first (Photino on Linux). The Razor class library is host-agnostic, so the Android
host is additive later, not a rewrite — don't build phone-only code paths ahead of that phase.

### Solution layout

```
MyBudget.slnx
src/
  MyBudget.Domain          entities and value objects, no dependencies
  MyBudget.Engines.Ledger  pure calculations for Phase 1 (pay dates, sinking-fund accrual, transfer needs)
  MyBudget.Contracts       DTOs shared by API and UI
  MyBudget.Data            EF Core DbContext, provider switch, seed data
  MyBudget.Api             ASP.NET Core minimal APIs
  MyBudget.UI              Razor class library: pages, components, API client, stylesheet
  MyBudget.Desktop         Photino.Blazor host (Linux)
tests/
  MyBudget.Engines.Tests   engine tests against real numbers
  MyBudget.Api.Tests       endpoint tests over the in-memory provider
```

Later engines (PaycheckEngine, HsaPlanner, RewardsOptimizer, Amortization) each get their own
`MyBudget.Engines.*` project with the same no-UI/no-DB rule.

### Data model shape

Every fact carries an effective date or period rather than living in a year-specific table — there
is no "2026 tab" equivalent. Tax brackets, HSA limits, and 401(k)/IRA limits live in reference
tables keyed by year so they update without a code change each January.

## Open Questions

- Hostname/credentials of the existing PostgreSQL server and which Linux server hosts the API.
- Whether the in-memory dev provider should move to SQLite once the model stabilizes (ADR-0005).

## Status

Architecture decided. Solution skeleton and Phase 1 core ledger built 2026-09-26 (see DD-0002). This DD will be superseded/expanded as subsystem DDs are
written for the paycheck engine, HSA planner, rewards optimizer, and investment tracking.

## References

- ADR-0001: No paid account aggregation
- ADR-0002: MAUI Blazor Hybrid for shared desktop/phone UI
- ADR-0003: Local AI restricted to tool-calling


## Desktop-first: the API runs in-process (2026-09-27, ADR-0010)

The shape changed. The desktop app is the whole application: it starts an ASP.NET host inside its own
process on a loopback port and serves the same endpoints against a SQLite file under the user's local
data folder. There is no service to install and no database server.

```
MyBudget.Desktop (one process)
  ├── Photino window  ──►  MyBudget.UI (Razor)  ──►  ApiClient (HTTP to 127.0.0.1:<ephemeral>)
  └── BudgetApiHost   ──►  minimal APIs  ──►  EF Core  ──►  ~/.local/share/MyBudget/mybudget.db
```

`BudgetApiHost` holds the service registration and endpoint mapping that both entry points share:
`MyBudget.Api`'s own `Program` (still a standalone executable, kept for the phone-sync server) and
`LocalApi` in the desktop app. The port is taken from the OS at startup and the API key is generated
per run, so neither is fixed or guessable.

The HTTP hop was kept deliberately rather than refactoring the UI to call services directly. It costs
almost nothing on loopback and it means the phone gets a working API rather than a port of one.

Set `MYBUDGET_API_URL` and the desktop app talks to a remote API instead of starting its own, which
is the path to a shared server when one exists.

**Schema changes need a migration.** The file holds real data now, so `Database.MigrateAsync` runs at
startup for any relational provider. `EnsureCreated` would silently stop matching the model and take
the data with it. Tests still use the in-memory provider, which has no migrations and needs none.


## Export and restore (2026-09-27)

A whole budget is exported and restored as the SQLite database file itself, not a JSON dump.

Nearly every table points at another by id: a transaction at a card, a loan at an asset, a budget
line at a label, an earn rule at a category and a label. A dump-and-reload would have to renumber
every key and rewrite every reference that uses it, and the failure mode of getting that wrong is
silent — wrong numbers attached to the right-looking rows, on real data. A database copy is exact by
construction and needs no mapping at all.

- **Export** uses `VACUUM INTO`, which produces a consistent, compacted copy including anything still
  in the write-ahead log. Never a raw file copy of a live database.
- **Restore** inspects the upload first, requiring `__EFMigrationsHistory` plus the core tables, and
  reports what it contains before anything is replaced. The current budget is copied beside it, then
  the upload is staged as `mybudget.db.pending`.
- **The swap happens at startup**, in `LocalApi.ApplyPendingRestore`, when nothing holds the file open.
  The old `-wal` and `-shm` sidecars are removed with it, or they would be read against the new file.
- Migrations then run as usual, so a budget exported from an older version comes forward on load.

Only available on SQLite; the endpoints refuse for any other provider, and a refusal is a normal
result object rather than an error so the page always has something to show.
