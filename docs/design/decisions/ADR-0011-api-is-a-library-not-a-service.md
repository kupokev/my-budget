# ADR-0011: MyBudget.Api is a library, not a runnable service

> **Status:** Accepted
> **Date:** 2026-09-27
> **Deciders:** Kevin
>
> **Supersedes the "stays runnable standalone" part of:** ADR-0010

## Context

ADR-0010 moved the app to a single process: the desktop host builds the API in-process on a loopback
port against a SQLite file. It deliberately kept `MyBudget.Api` runnable on its own — its own
`Program.cs`, `launchSettings.json` with ports 5210/7210, and `appsettings.json` /
`appsettings.Development.json` — so that a future phone could sync against a real server without
restructuring anything.

That hedge cost something every day it sat there:

- Two config files that nothing in the shipping app read, yet were copied into the desktop app's
  output. One of them declared `Database:Provider = Sqlite` with `Data Source=mybudget.db`, a path
  that never applies, because the desktop passes the real path in explicitly.
- A `Microsoft.AspNetCore.OpenApi` reference and `AddOpenApi`/`MapOpenApi` calls used only by the
  standalone dev server.
- A redundant `Microsoft.EntityFrameworkCore.Design` reference; migrations are run against
  `MyBudget.Data`, which has its own.
- A `UserSecretsId` for a server that does not exist.
- Documentation describing a `dotnet run --project src/MyBudget.Api` workflow, which made it look
  like there was a service to operate.

Asked directly whether to keep the hedge, the answer was to drop it: the code should be as clean as
possible now, and the server can be rebuilt when a phone actually needs one.

## Decision

`MyBudget.Api` becomes a plain class library (`Microsoft.NET.Sdk` plus a `FrameworkReference` to
`Microsoft.AspNetCore.App`). It exposes `BudgetApiHost.CreateBuilder`, `PrepareDatabaseAsync` and
`MapBudgetApi`, and nothing else. There is no entry point, no `appsettings`, no launch profile and no
port.

The one thing that entry point was genuinely load-bearing for was the tests, which booted it through
`WebApplicationFactory<Program>`. `ApiFixture` now assembles the API the same way the desktop app
does and runs it over `Microsoft.AspNetCore.TestHost`'s in-memory transport, so
`Microsoft.AspNetCore.Mvc.Testing` is replaced by the smaller `TestHost`. Test configuration
(in-memory provider, per-class database name, API key) is stated in the fixture instead of being read
out of an `appsettings.Development.json` that only tests loaded.

## Consequences

### Positive

- One way to run the app, and the tests exercise the same assembly path the desktop app uses.
- Four files, two packages and a redundant one deleted; nothing that ships describes a server.
- Test dependencies are visible in the fixture rather than hidden in JSON next to production config.

### Negative

- Building a phone-sync server later means writing a host again: a `Program.cs` that calls
  `CreateBuilder` / `MapBudgetApi`, plus whatever config it needs. That is a small file, because the
  endpoints and services it would host are untouched and still factored for exactly this.
- A plain library does not get the Web SDK's implicit `using` directives, so the nine namespaces it
  used to supply are declared once in the csproj.

### Risks

- A future server would need its own migration set if it targets PostgreSQL rather than SQLite; that
  risk is unchanged from ADR-0010, only deferred further.

## Alternatives Considered

### Keep the standalone host

Rejected as the hedge described above. Its only current consumer was the test fixture, and replacing
that took fewer lines than the scaffolding it removed.

### Delete MyBudget.Api entirely

Not possible, and worth recording so it is not proposed again: the project *is* the backend — every
endpoint plus `AiService`, `BackupService`, `InvestmentService`, `ImportService`, `PaycheckService`,
`AlertsService`, `MarketData` and `ApiKeyMiddleware`. The desktop app calls directly into it.

### Move the endpoints into MyBudget.Desktop

Rejected. It would couple the endpoints to the Photino host, make them untestable without a window,
and delete the seam that keeps the UI talking HTTP to a swappable address.

## References

- ADR-0010 — SQLite, API in-process, desktop first (this narrows it)
- ADR-0006 — auth scope; the API key is now generated per launch and never leaves the process
- `src/MyBudget.Api/BudgetApiHost.cs`, `src/MyBudget.Desktop/LocalApi.cs`, `tests/MyBudget.Api.Tests/ApiFixture.cs`
