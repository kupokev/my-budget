# DD-0008: Build-out — alerts, home dashboard, receivables, 1099, rainy-day (Phase 5)

> **Status:** Active
> **Last updated:** 2026-09-26

## Alerts (ALT-1..6) — `AlertsService`

Computed on request. Home shows the financial-health ones; rewards alerts (`Kind = "rewards"`) stay off Home and appear as a collapsed heads-up on the Cards page, since rewards are something Kevin digs into on purpose rather than gets nagged about. Transfer short + bill due within 7 days (ALT-1);
balance below next-14-days bills plus the minimum (ALT-2); rewards goal gaps and thresholds off
pace (ALT-3); HSA behind straight-line pace (ALT-4); open wash-sale windows (ALT-5); annual fee
within 45 days (ALT-6); plus uncategorized transactions and unverified/missing tax tables.

## Home (HOME-1, full)

`home/dashboard` assembles alerts, bills due, next paycheck, transfer needs, spending this vs last
month, net worth and month change, rainy-day months covered, goals, and (when enabled) the AI
monthly narrative. The page shows tiles (net worth first, centered, spending tile links to Spending),
then bills due and goals; per-category spending and status goals are left to their own pages.

## Receivables (DBT-2a–c)

`Person` → `Obligation` (fixed monthly amount or a share of a bill's projected amount, with a
start/end month), `ReceivableCharge` (one-offs), `ReceivablePayment` with `PaymentAllocation`s to
months or to the one-off balance. The ledger walks each month from the first obligation to the
latest prepaid month: expected, paid, running balance, status (Paid / Partial / Missed / Due /
Prepaid / Upcoming), then one-off charged vs paid, unallocated receipts, total owed. "Auto" on the
page applies a payment to the oldest unpaid months first.

## 1099 set-aside (INC-8) — `SelfEmployment` in the paycheck engine

Receipts per 1099 source; projected net profit = YTD annualized or a typed figure; SE tax on 92.35%
of profit with the Social Security base reduced by W-2 wages; half-SE deduction; income tax at the
marginal rate from the W-2 estimate; Missouri at its top rate; set-aside fraction; remaining
balance after estimated payments spread over the due dates still ahead (Apr 15, Jun 15, Sep 15,
Jan 15).

## Rainy-day fund (ACC-5)

Monthly expenses = bill accruals + planned variable spend; marked accounts' latest balances; months
covered vs the 3–6 month band, with a verdict.

## Not yet built (still Phase 5)

Reimbursables (INC-10), employer profit sharing (INC-11), crypto and home-improvement side modules
(RPT-3), bill notes history (BIL-6), calendar view (BIL-5), card utilization by reporting day
(CC-3), HSA last-month rule (HSA-5).
