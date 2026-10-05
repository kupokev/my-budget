# Design index

Subsystem map for MyBudget. Each row is a living Design Document — the spec for how that piece
works. Add a row and a DD when a new subsystem gets its own design.

| DD | Subsystem | Status |
| --- | --- | --- |
| [DD-0001](./DD-0001-architecture-overview.md) | Architecture overview | Active |
| [DD-0002](./DD-0002-core-ledger.md) | Core ledger: income, accounts, bills, cards, transfer needs, home view (Phase 1) | Active |
| [DD-0003](./DD-0003-calculators.md) | Calculators: paycheck estimator, HSA planner, amortization, reference tables (Phase 2) | Active |
| [DD-0004](./DD-0004-rewards-optimizer.md) | Rewards and status optimizer: programs, paths, thresholds, spend plan, card catalog (Phase 3) | Active |
| [DD-0005](./DD-0005-import-and-history.md) | Import and history: statement import, transactions, spending dashboard, goals, year-over-year, export (Phase 4) | Active |
| [DD-0006](./DD-0006-investments.md) | Investments: lots, dividends + DRIP, gains and tax, wash sales, assets (Phase 5) | Active |
| [DD-0007](./DD-0007-local-ai.md) | Local AI on Ollama, tool-calling only (Phase 5) | Active |
| [DD-0008](./DD-0008-buildout.md) | Build-out: alerts, home dashboard, receivables, 1099 set-aside, rainy-day fund (Phase 5) | Active |
| [DD-0009](./DD-0009-income-and-pay-allocation.md) | Income: salary and schedule history, deposit allocation and its versions, matching deposits to a cheque | Draft |
| [DD-0010](./DD-0010-paid-time-off.md) | Paid time off: buckets per job, balances from stubs, projection, goals that need days off | Active |

## Decision records

| ADR | Decision |
| --- | --- |
| [ADR-0001](./decisions/ADR-0001-no-paid-account-aggregation.md) | No paid account aggregation; CSV/OFX import |
| [ADR-0002](./decisions/ADR-0002-maui-blazor-hybrid-shared-ui.md) | Shared Razor UI (desktop host part superseded by ADR-0004) |
| [ADR-0003](./decisions/ADR-0003-local-ai-tool-calling-only.md) | Local AI restricted to tool-calling |
| [ADR-0004](./decisions/ADR-0004-photino-desktop-host-on-linux.md) | Photino.Blazor desktop host on Linux; MAUI only for Android |
| [ADR-0005](./decisions/ADR-0005-no-docker-systemd-and-existing-postgres.md) | No Docker; systemd + PostgreSQL in prod, in-memory in dev |
| [ADR-0006](./decisions/ADR-0006-lan-only-api-key-tls-no-mfa.md) | LAN-only, single API key, TLS, no MFA |
| [ADR-0007](./decisions/ADR-0007-no-ui-component-framework.md) | No UI component framework; hand CSS + QuickGrid |
| [ADR-0008](./decisions/ADR-0008-store-full-account-numbers.md) | Store full account/member/loan numbers on bills, loans, cards, accounts |
| [ADR-0009](./decisions/ADR-0009-market-data-from-yahoo-chart-endpoint.md) | Market data from Yahoo's free chart endpoint behind a provider interface |
| [ADR-0010](./decisions/ADR-0010-sqlite-in-process-desktop-first.md) | SQLite file, API hosted in-process by the desktop app, no server |
| [ADR-0011](./decisions/ADR-0011-api-is-a-library-not-a-service.md) | `MyBudget.Api` is a library, not a runnable service (narrows ADR-0010) |
| [ADR-0012](./decisions/ADR-0012-every-row-records-created-and-updated.md) | Every row records when it was created and last changed |

Files are named `DD-NNNN-name.md` and `ADR-NNNN-name.md` so the number in prose maps straight to a file.
