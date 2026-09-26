# Design index

Subsystem map for MyBudget. Each row is a living Design Document — the spec for how that piece
works. Add a row and a DD when a new subsystem gets its own design.

| DD | Subsystem | Status |
| --- | --- | --- |
| [DD-0001](./DD-0001-architecture-overview.md) | Architecture overview | Active |
| [DD-0002](./DD-0002-core-ledger.md) | Core ledger: income, accounts, bills, cards, transfer needs, home view (Phase 1) | Active |

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

Files are named `DD-NNNN-name.md` and `ADR-NNNN-name.md` so the number in prose maps straight to a file.
