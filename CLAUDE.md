# MyBudget

Personal budget app replacing a 2013–2026 spreadsheet. Single user (Kevin), not a Fact Foundry
product — no multi-tenant or productization concerns anywhere in this codebase.

## What it does

Tracks bills, accounts, credit cards, and debts like a normal budget app, plus three things a
spreadsheet can't do well:
- **Paycheck estimator** — federal + Missouri withholding, per check and per year, including
  bonuses/supplemental pay and 1099 income.
- **HSA planner** — per-month eligibility, annual limit, recommended contribution pace.
- **Rewards optimizer** — credit card spend thresholds and loyalty status progress (IHG, Hilton),
  weighed against a per-bill bank-autopay discount where one exists.

It also tracks investment holdings (manual trade entry, automatic dividend/price fetch, DRIP,
short/long-term gain classification, wash-sale checks) and offers optional local-AI features
(monthly narrative summary, a data Q&A chat) running against the user's own Ollama instance.

The full requirements/design doc this project was scoped from lives outside this repo (a Claude
Docs document); this CLAUDE.md and `docs/` are the durable, in-repo record going forward — update
them as the design evolves, don't treat the original doc as authoritative once they diverge.

## Engineering principles

- **Calculation engines are pure libraries** (PaycheckEngine, HsaPlanner, RewardsOptimizer,
  Amortization) — no UI or DB dependencies, unit-tested against real pay stubs and real HSA/rewards
  numbers, not synthetic fixtures. If a number doesn't match a real stub, that's a bug.
- **Auditability** — every calculated number must be traceable to its inputs and formula. Don't
  build a calculation whose reasoning can't be displayed.
- **No paid integrations** — no Plaid/SimpleFIN/aggregator, no paid market-data tier, no paid
  valuation API. Import is CSV/OFX statement files; market data is a free-tier API; home/vehicle
  value is manual entry. If a feature needs a paid service, it doesn't ship until that's revisited.
- **AI is tool-calling only** — the local model (Ollama, on your own hardware) never writes or runs its own
  database query. It calls a small set of fixed, tested functions (spend by category, account
  balance, rewards progress, etc.) and narrates only what those return. A question outside that set
  gets "can't answer that yet," never a guess. New functions get added deliberately, not invented by
  the model at runtime.
- **Desktop first, Linux only** — one shared Razor class library (`MyBudget.UI`) hosted by
  Photino.Blazor on Linux. Android (MAUI Blazor Hybrid host) is a later phase using the same UI
  and API; don't build phone-only code paths ahead of that. No Windows/macOS builds.
- **No Docker, no component framework** — the API runs under systemd on a Linux server; the UI is
  hand-written CSS plus QuickGrid. Dev uses the EF Core in-memory provider, production uses
  PostgreSQL (ADR-0005, ADR-0007).

## Tech stack

| Layer | Choice |
| --- | --- |
| UI | Razor class library; Photino.Blazor host on Linux desktop now, MAUI Android host later (ADR-0004) |
| API | ASP.NET Core minimal APIs |
| Data | EF Core: in-memory provider in dev, PostgreSQL (existing network server) in prod (ADR-0005) |
| Charts | FactFoundry.Blazor.Charts |
| Styling | Hand-written CSS + QuickGrid, no component framework (ADR-0007) |
| Auth | LAN-only, single API key, TLS in transit, no MFA (ADR-0006); full account numbers stored (ADR-0008) |
| Import | CSV/OFX per institution |
| Local AI | Ollama + Open WebUI (an instance you already run), tool-calling model |
| Hosting | `dotnet publish` + systemd on a Linux server on the home network, no Docker |

## Working on the code

```
dotnet build                                   # whole solution (MyBudget.slnx)
dotnet test                                    # engine + API tests, no database needed
dotnet run --project src/MyBudget.Api --launch-profile http   # API on http://localhost:5210, in-memory DB, dev seed, key "dev"
dotnet run --project src/MyBudget.Desktop      # Photino desktop app; MYBUDGET_API_URL / MYBUDGET_API_KEY override defaults
```

Layout: `src/MyBudget.{Domain,Engines.Ledger,Engines.Paycheck,Engines.Hsa,Engines.Amortization,Engines.Rewards,Contracts,Data,Api,UI,Desktop}`, `tests/MyBudget.{Engines,Api}.Tests`.
Central package versions live in `Directory.Packages.props`. `AllowMissingPrunePackageData` in
`Directory.Build.props` works around the Arch-packaged SDK (NETSDK1226). Engines never reference
Data, Api, or UI. The UI never references Data or Api directly, only Contracts and its own `ApiClient`.

## Docs structure

```
docs/
  design/
    DESIGN.md          — index of subsystems, links to DD docs below
    DD-TEMPLATE.md      — copy this to start a new Design Document
    DD-XXXX-*.md        — living specs, one per subsystem/feature, updated as design evolves
    decisions/
      ADR-TEMPLATE.md   — copy this to record a new decision
      ADR-NNNN-*.md     — frozen decision records (referenced in prose as ADR-NNNN)
  reference/            — API/tool contracts, calculation rule references, glossary
  user-guide/           — how to actually use the app day to day
  troubleshooting/      — known problems and their fixes
  distribution/         — build, packaging, and release notes (MAUI publish steps, etc.)
  audit/                — verification of calculated numbers against real stubs/statements
  Known Issues.md
  Roadmap.md
  Future Enhancements.md
```

**DD vs ADR:** a Design Document (`design/DD-XXXX-*.md`) is the living spec for how a subsystem
works — update it as the design changes. An ADR (`design/decisions/ADR-NNNN-*.md`) is a frozen record of
one decision and the alternatives rejected — never edit an ADR after the fact; supersede it with a
new one instead. A DD often points back to the ADR(s) that justify its design; an ADR often points
forward to the DD it feeds.

Start with `docs/design/DESIGN.md` for the current subsystem map, and `docs/design/DD-0001-architecture-overview.md`
plus the first few ADRs in `docs/design/decisions/` for how the stack and the no-paid-integration /
tool-calling-AI constraints were decided.
