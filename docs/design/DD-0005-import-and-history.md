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
| `Transaction` | A line on an account or card. Signed amount, description, normalised merchant, category, matched bill, transfer flag, external id (unique per source), manual-categorization flag, **origin** (Imported or Manual). Manual transfers between your accounts are two Manual rows linked by `LinkedTransactionId`. **Reconciliation:** on commit each imported line links (`ReconciledWithId`, both ways) to an unreconciled manual row on the same source with the same amount within 3 days, inheriting its transfer flag, counterparty, category and bill; a reconciled manual row no longer counts toward balances or transfer totals, so the pair counts once. Unreconciled manual rows are marked on the Transactions page and can be reconciled by hand from a candidate list (same source, same sign, ±45 days) or unlinked |
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

- Spending = money out, non-transfer. The `spending/*` endpoints remain, because the AI tool
  `spend_by_category` and the dashboard tile read them, but **there is no Spending page** (see below).
- Goal metrics: net worth (accounts − cards − loans from latest balances), selected account
  balances, contributions into an account type (HSA reads its own contribution records; every other
  type sums money into accounts of that type between the dates), 401(k) deferrals estimated from
  paychecks, category inflow/outflow between the dates, loan balance, or manual. Prorated target = start + (target −
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
(month/year, filters, inline edit with "always", rules panel), Goals (progress bars with elapsed tick, editor),
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


## The Spending page was removed (2026-09-27)

It showed a category table for this month versus last, a category-by-month matrix, tiles for
month/year-to-date/income, and a drill-down to merchants and transactions. Every analytical part of
that turned out to live somewhere better:

- The category-by-month matrix is Reports' year-over-year grid, which draws the same thing with the
  prior year beside it.
- Budget versus actual per category is the Budget page with its category filter applied: the totals
  row already respects the filter, so it gives projected and actual per month for that category.
- The uncategorized count is already a Home alert and already on the Home spending tile.
- The transaction detail is the Transactions page.

What replaced it is smaller and sits where the question gets asked. The Budget page's hovers now
carry last year's actuals: the line-name tooltip shows last year's total, the months it was charged
and the average, with this year so far beneath it; each month cell shows the same month a year
earlier. The page therefore loads both this year's and last year's history.

The `spending/*` API endpoints stayed. The AI's `spend_by_category` tool calls
`SpendingEndpoints.Summary` directly, and the dashboard tile uses the summary figures.


## Transactions are created in one place (2026-09-27)

`POST /api/transactions` is the only way to create one by hand. It takes an account **or** a card,
never both, and an optional counterparty account, which writes the mirror row and links the pair.
The Transactions page has the editor; it is the single place transactions are entered.

Two other places used to create them and no longer do. The rewards card-spend grid wrote monthly
aggregates into a separate table (now derived, see DD-0004). The Accounts ledger had a form that
recorded transfers as well as statement balances; it keeps the balances, which are reconciliation
points rather than movements, and points at Transactions for anything that moves money.
