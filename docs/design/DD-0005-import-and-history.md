# DD-0005: Import and history (Phase 4)

> **Status:** Active
> **Last updated:** 2026-09-26
> **Related ADRs:** ADR-0001 (statement upload, no aggregator), ADR-0008 (account numbers as future match keys)

## Context / Problem

Everything before this phase is planned numbers. Phase 4 brings actuals in from statements
(BIL-7/8/9), turns them into the spending dashboard the sheet never had (RPT-0), fills bill actuals
and card spend from real lines, makes goals fill themselves (GOL-1/2/3), and compares years (RPT-1)
with CSV export (RPT-2). No workbook importer: the decision was to import history via statements.

## Design

### Import engine (`MyBudget.Engines.Import`, pure)

- `CsvStatementParser`: RFC-4180-ish reader; `CsvProfiles` for Chase card, Chase checking,
  Capital One, Amex, Discover, Citi, PNC, Wells Fargo (header-less) and a generic layout, detected
  from the header. Amounts are normalised to "money out is negative" (Amex and Discover list
  charges positive; debit/credit pairs are combined). Dates try the common US formats.
- `OfxStatementParser`: tolerant SGML/XML OFX and QFX; reads every STMTTRN (DTUSER preferred
  over DTPOSTED for the date, FITID as the external id).
- `Merchants.Normalize`: strips processor prefixes (SQ *, TST*, PAYPAL *), order codes after a
  star, phone numbers, store numbers, dates and trailing "CITY ST", then title-cases. Same input
  always gives the same output, which is all grouping needs.
- `Merchants.HashId`: SHA-256 of source|date|amount|description|occurrence for CSV lines with no
  institution id, so re-importing the same file finds duplicates and two identical lines on one
  day stay distinct.

### Data

| Entity | Purpose |
| --- | --- |
| `ImportBatch` | One uploaded file: source, layout, counts, date range. Deleting it removes its lines |
| `Transaction` | A line on an account or card. Signed amount, description, normalised merchant, category, matched bill, transfer flag, external id (unique per source), manual-categorization flag |
| `CategoryRule` | Pattern (contains / starts-with / regex) → category, bill, transfer; priority order |
| `Goal` | Financial (metric-driven) or non-financial (status); target, start value, dates, lower-is-better |

### Import flow

Preview: parse → normalise merchants → compute external ids → mark duplicates against the
source's existing lines → suggest for each line, in order: a matching rule; a payment/transfer
hint in the description; a bill whose name appears in the description (money out only); the
statement's own category text matched to an app category. The user corrects category, bill,
transfer, skip in the grid, then commits. Commit inserts non-duplicate rows and **syncs**:

- Bill actuals (BIL-3): for each (bill, month) touched, `BillPeriod.ActualAmount` = Σ money-out
  lines matched to that bill; noted "from import".
- Card spend (RWD-3): for each (card, month) touched, `CardSpend` rows are replaced by sums per
  category of non-transfer money-out lines. Manual rows for months without lines survive.

The same sync runs when a line is edited, deleted, or a batch is undone. Editing a line marks it
manually categorized; "always" creates a Contains rule on the merchant and applies it to all
non-manual lines.

### Spending, goals, reports

- Spending = money out, non-transfer. Summary: this month vs last per category, YTD, average,
  uncategorized count; matrix: category × month; drill-down: merchants and lines.
- Goal metrics: net worth (accounts − cards − loans from latest balances), selected account
  balances, HSA contributed in the goal's year, 401(k) deferrals estimated from paychecks, category
  inflow/outflow between the dates, loan balance, or manual. Prorated target = start + (target −
  start) × elapsed/total days. Status: Done, Exceeded (reached before the end date), On Track,
  Not On Track; lower-is-better flips the comparisons.
- Year over year: categories from transactions, bills from BillPeriod actuals, both by month.
- Net worth: latest balance on or before a date per account/card/loan, plus 24 monthly points.
  Home and vehicle values (ACC-4a) are not yet tracked.
- Export: transactions, bills grid, accounts as CSV through a host-provided save dialog
  (`IFileSaver`, Photino on desktop).

### API and UI

`import/profiles`, `import/preview` (multipart), `import/commit`, `import/batches` (+ DELETE);
`transactions` (filters, PUT with rule creation, DELETE); `category-rules` CRUD + `apply`;
`spending/summary`, `spending/matrix`, `spending/category/{id}`; `goals` CRUD + `progress`;
`reports/year-over-year`, `reports/net-worth`; `export/*.csv`.

Pages: Import (source, layout, file, preview grid, previous imports with undo), Transactions
(month/year, filters, inline edit with "always", rules panel), Spending (tiles, category table
with drill-down, month-over-month matrix), Goals (progress bars with elapsed tick, editor),
Reports (year over year, net worth with history, export).

## Open Questions

- Institution CSV layouts drift; a mismatch shows up as a parse warning and needs a profile edit.
- Category rules are Contains on the normalised merchant by default; regex is available for
  awkward cases.
- Transactions that pay a card from a bank account and the card's own "payment" line are both
  transfers; balances are still snapshots, not derived from lines.

## Status

Built 2026-09-26: engine (8 tests), API (4 tests), five pages, file saver on the desktop host.

## References

- DD-0002 (bill actuals), DD-0004 (card spend), requirements BIL-7–9, GOL-1–3, RPT-0–2, ACC-4
