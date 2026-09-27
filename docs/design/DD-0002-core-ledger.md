# DD-0002: Core ledger (Phase 1)

> **Status:** Active
> **Last updated:** 2026-09-26
> **Related ADRs:** ADR-0001, ADR-0004, ADR-0005, ADR-0006, ADR-0007

## Context / Problem

Phase 1 replaces the yearly workbook's income header, account funding grid, bill grid, and card
grid with one model (INC-1–3, BIL-1–4, ACC-1–3, CC-1–2), plus a first cut of the home view
(HOME-1) so the landing screen exists from the start. Done when October–December 2026 can be run in
the app instead of the 2026 tab.

## Design

### Entities

| Entity | Purpose | Notes |
| --- | --- | --- |
| `IncomeSource` | Employer or other money source (INC-1) | Type: W-2, 1099, reimbursement, other |
| `SalaryRate` | Annual salary from an effective date (INC-2) | A raise is a new row; unique per (source, date) |
| `PaySchedule` | Pay cadence from an effective date (INC-3) | Bi-weekly/semi-monthly/monthly; anchor pay date; semi-monthly days (31 = last); weekend → prior Friday flag. Effective-dated because 2026 switched 24 → 26 mid-year |
| `Account` | Where money sits (ACC-1) | Type, min balance, transfer cadence (monthly / per paycheck), rainy-day flag, account number (ADR-0008) |
| `AccountBalance` | Balance snapshot as of a date (what the statement said) | Unique per (account, date). The balance shown anywhere = latest snapshot + transfers dated after it (`BalanceMath`), so recording a transfer moves both accounts immediately |
| (transfers) | Manual transfers are `Transaction` rows with Origin = Manual and IsTransfer = true (DD-0005); summed per month for Long/Short | One table for every money movement |
| `Card` | Credit card (CC-1) | APR + promo, statement/due day, limit, fee + month, paying account |
| `CardBalance` | Statement/month-end balance (CC-3/4 later) | Unique per (card, date) |
| `BudgetLine` | Anything you plan to spend money on. A dated obligation (mortgage, insurance) or, with `BudgetFrequency.Variable`, planned spend with no due date and no biller (groceries, restaurants, gas). Optionally carries a `Label` so one merchant can be budgeted apart from its category |
| `Category` | Spending bucket (BIL-8 later) | Budget lines carry one; imported transactions do too |
| `BudgetLine` | Obligation (BIL-1/2) | Account/member number (ADR-0008), frequency, due day, anchor due date for non-monthly, autopay, projected, **payment method** (account or card) and **funding account** (always set), optional bank-autopay discount (RWD-4a, stored only) |
| `BudgetPeriod` | One bill in one month (BIL-3) | Unique per (bill, month). Holds the actual plus optional **per-month overrides** for due date and expected amount, since real bills drift month to month; null override = bill default. A row may hold only an override. Empty row is deleted on save |

Every fact is effective-dated or period-keyed; there is no year table (DD-0001).

### Calculations (`MyBudget.Engines.Ledger`, pure)

| Function | Rule | Requirement |
| --- | --- | --- |
| `PayDates.Generate` | For each date the schedule in effect is the latest EffectiveDate ≤ date. Bi-weekly steps 14 days from the anchor; monthly uses the anchor's day clamped to month end; semi-monthly uses the two days. Weekend dates shift to the prior Friday when the flag is on | INC-3 |
| `PayDates.ThreePaycheckMonths` | Months with ≥ 3 pay dates, with the dates | INC-4 |
| `BudgetDueDates.Between` | Monthly: due day each month (clamped). Quarterly/semi-annual/annual: step 3/6/12 months from the anchor in both directions. One-off: the anchor. Honors start/end/active. Optional per-month due-date overrides replace that month's date, even across the window edge | BIL-1, BIL-5 |
| `SinkingFund.MonthlyAccrual` | Monthly → amount (or this month's override, formula says so). Q/SA/A → amount × occurrences ÷ 12. One-off → amount ÷ whole months until due. Returns the formula string with the number | BIL-4 |
| `TransferNeeds.Compute` | Group active bills by **funding account** (never the card); monthly need = Σ accruals; per-paycheck = monthly × 12 ÷ paychecks/year; each line carries its formula | ACC-2, ACC-2a |

Long/Short (ACC-3) is computed in the API: transfers recorded this month − monthly need.

The Accounts page shows a different number, **this month's cash outflow** per account: bills paid
from the account directly plus bills charged to a card that the account pays, at the month's
projected amounts. That is what a direct deposit must cover; the funding-account transfer needs
(with formulas) stay on the Dashboard.

Paychecks per year for the per-paycheck figure comes from the active W-2 source's schedule in
effect on the as-of date, falling back to 12 with an explanatory string when none exists. The
source string is returned with the numbers so the UI can show it.

### API (`/api`, key required; `/health` anonymous)

| Route | Purpose |
| --- | --- |
| `accounts` CRUD, `accounts/{id}/balances`, `accounts/{id}/transfers` | ACC-1, ACC-3 |
| `cards` CRUD, `cards/summary`, `cards/{id}/balances` | CC-1, CC-2 |
| `categories` list/create/update | BIL-8 groundwork |
| `bills` CRUD, `bills/history?year=` (per month: due date, expected, actual, variance, override flags), `bills/{id}/periods/{period}` (PUT upsert / DELETE), `bills/upcoming?days=` (uses overrides) | BIL-1–4 |
| `income-sources` CRUD (rates and schedules replaced wholesale), `income-sources/pay-calendar?year=` | INC-1–4 |
| `transfer-needs?asOf=` | ACC-2/2a/3 with per-line formulas |
| `home` | Upcoming bills (14 days), next pay date, transfer needs |

Deleting an account or card that a bill references returns 409; mark it inactive instead.
Enums serialize as strings.

### UI (`MyBudget.UI`, Razor class library)

Pages: Home, Budget (year grid with prev/next year, optional Category/Due/Paid via/Funded from columns
hidden by default behind a Columns checklist and shown on a hover card over the bill name; clicking a
month opens a month editor for actual, expected-this-month, due-this-month, paid-on, notes; bill
editor, categories), Accounts (list, editor, balance/transfer ledger, transfer-needs breakdown with
formulas), Cards (summary with bills-on-card and utilization, editor, balance entry), Income
(sources with salary history and effective-dated schedules, pay-date calendar with 3-check months).

One stylesheet (`wwwroot/app.css`), light/dark via `prefers-color-scheme`, no framework (ADR-0007).
Every computed number's formula is visible next to it (auditability principle).

### Hosts

`MyBudget.Desktop` is Photino.Blazor (ADR-0004). It reads `MYBUDGET_API_URL` (default
`http://localhost:5210`) and `MYBUDGET_API_KEY` (default `dev`). On Wayland it forces
`GDK_BACKEND=x11` unless already set, as semantic-modeler does.

## Open Questions

- Real 2026 pay-schedule anchors and the actual list of open cards; the dev seed uses placeholders.
- Whether one-off bills should accrue at all or just appear as due items.
- Column visibility on Budget is per session; persist it (per-viewer local storage) if it gets annoying.
- Card balance history (CC-3/4) needs a statement-import path or manual monthly entry habit.

## Status

Built 2026-09-26: entities, engine (15 tests), API (8 endpoint tests), five pages, Photino host.
Same day: per-month due/expected overrides, Budget column toggles and hover card, year arrows, full-width layout.
Not yet run against PostgreSQL; no migrations generated yet (ADR-0005 dev path only).

## References

- DD-0001 architecture overview
- `src/MyBudget.Engines.Ledger/`, `src/MyBudget.Api/Endpoints/`, `src/MyBudget.UI/Pages/`
- `tests/MyBudget.Engines.Tests/`, `tests/MyBudget.Api.Tests/`


## Budget, not bills (2026-09-27)

The page and the entity were called `Bill`, and planned variable spend lived separately as a
`PlannedMonthly` amount on `Category` and `Label`. That was two models for one idea. Groceries is
something you plan to spend money on, exactly like the mortgage is; the only difference is that it has
no due date and no biller.

So `Bill` became `BudgetLine`, `BudgetFrequency.Variable` was added for undated planned spend, and
`Category.PlannedMonthly` / `Label.PlannedMonthly` were removed. The Categories screen still shows a
planned figure per category, but it reads it back from the budget rather than owning it. A budget line
can now carry a `LabelId`, which is what lets Amazon be budgeted and routed apart from the rest of
General merchandise; that was previously the label's own planned amount, carved out of its category's.

The payoff is that the rewards spend plan draws its whole pool from one place. It used to add
card-eligible bill accruals to category planned amounts and then carve label amounts back out of
those. Now it sums card-eligible budget lines, keyed by (category, label), and reports the split as
dated versus variable.
