# Distribution

Build, packaging, and release notes: Photino desktop publish steps (Linux), the API as a
`dotnet publish` output run under systemd on a Linux server (no Docker, ADR-0005), PostgreSQL
migration process, TLS certificate setup (ADR-0006), and how a new version gets onto the server.

## Current state (2026-09-26)

Nothing is published yet. Intended shape:

- **API:** `dotnet publish src/MyBudget.Api -c Release -r linux-x64 --self-contained false -o /opt/mybudget/api`
  on the server, with `appsettings.Production.json` (or environment variables) setting
  `Database:Provider=PostgreSQL`, `Database:ConnectionString`, `Api:Key`, and Kestrel's HTTPS
  certificate. Run under a systemd unit (`mybudget-api.service`) as its own user. Run
  `dotnet ef database update` (or apply the SQL script) before the first start of each release;
  migrations are not generated yet.
- **Desktop:** `dotnet publish src/MyBudget.Desktop -c Release -r linux-x64` and point
  `MYBUDGET_API_URL` / `MYBUDGET_API_KEY` at the server. Needs WebKitGTK on the machine.
