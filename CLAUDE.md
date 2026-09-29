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
- **One machine, one file** — the desktop app hosts the API in-process against a SQLite file; there is
  no server to run. A model change needs an EF migration, because the data in that file is real.
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
- **No Docker, no component framework** — there is nothing to deploy and no container; the app is
  a `dotnet publish` output you run. The UI is hand-written CSS and plain `<table>` markup, no
  component library (ADR-0007). Real data lives in SQLite; tests use the EF Core in-memory
  provider (ADR-0010, superseding the hosting half of ADR-0005).

## Forms and tables

A field added to a form is a design decision, not a drop. Before adding one:

- **Put it where it belongs.** Group it in the `<fieldset class="group">` whose legend describes it,
  next to the fields it relates to — a fee's month and its budget tick belong beside the fee, not at
  the end of the form.
- **`.field` is one caption and one control.** `.form .field label` is clipped to a single line, so
  explanation never goes in the label. Longer text is a `<span class="hint">`, which wraps under the
  control. A checkbox acting as the control gets `<label class="inline">` under a normal caption;
  standalone flags like "Active" go in the `.checks` column.
- **Keep columns narrow.** Show a short name in a table — `CardDto.Display` returns the nickname
  where there is one — and keep the full name for the editor and hovers.
- **Say what empty means.** An optional date uses `<OptionalDate Empty="no end" />` rather than a bare
  picker; an optional number says what leaving it blank does.
- **Close is the rightmost button.** In every modal footer the order is destructive action, then the
  primary action, then Cancel/Close on the far right. Consistency beats the usual convention here:
  knowing where the exit is without looking matters more than which button is emphasised.
- **A record can be corrected.** Anything logged — a use, a balance, a note — is editable in place.
  Making someone delete and retype a row to fix a typo destroys the record of something that happened.
- **Look at it.** A screenshot of a form with a field wrapping into its neighbour is a bug report, and
  it should not take one.

## Tech stack

| Layer | Choice |
| --- | --- |
| UI | Razor class library; Photino.Blazor host on Linux desktop now, MAUI Android host later (ADR-0004) |
| API | ASP.NET Core minimal APIs |
| Data | EF Core: SQLite file on the desktop, in-memory provider in tests (ADR-0010). No PostgreSQL provider is wired up today — a future sync server would add the package and one `case` back. |
| Charts | FactFoundry.Blazor.Charts |
| Styling | Hand-written CSS, no component framework (ADR-0007) |
| Auth | Loopback only, per-launch random API key, no MFA (ADR-0006 as narrowed by ADR-0010); full account numbers stored (ADR-0008) |
| Import | CSV/OFX per institution |
| Local AI | Ollama + Open WebUI (an instance you already run), tool-calling model |
| Hosting | None. `MyBudget.Api` is a library the desktop app hosts in-process on a loopback port (ADR-0010, ADR-0011). No entry point, no appsettings, no port to configure. |

## Working on the code

```
dotnet build                                   # whole solution (MyBudget.slnx)
dotnet test                                    # engine + API tests, no database needed
./run.sh                                       # stop, build, launch the desktop app (use this; it guards against a stale UI assembly)
dotnet run --project src/MyBudget.Desktop      # the app: hosts the API in-process against ~/.local/share/MyBudget/mybudget.db
dotnet ef migrations add <Name> --project src/MyBudget.Data --startup-project src/MyBudget.Data   # after any model change
```

Layout: `src/MyBudget.{Domain,Engines.Ledger,Engines.Paycheck,Engines.Hsa,Engines.Amortization,Engines.Rewards,Engines.Import,Engines.Investments,Contracts,Data,Api,UI,Desktop}`, `tests/MyBudget.{Engines,Api}.Tests`.
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
