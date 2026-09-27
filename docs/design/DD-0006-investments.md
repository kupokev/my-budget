# DD-0006: Investments (Phase 5)

> **Status:** Active
> **Last updated:** 2026-09-26
> **Related ADRs:** ADR-0009 (market data), ADR-0001

## Design

- **Entities:** `Holding` (ticker in a brokerage account, DRIP flag), `Trade` (buy / sell / reinvest;
  each buy is its own lot; reinvests link to their dividend), `DividendPayment` (ex-date, per-share,
  shares held the day before the ex-date, amount, fetched or manual), `PriceSnapshot` (ticker, date,
  close), `Asset` + `AssetValue` (home, vehicle, other; ACC-4a).
- **Engine (`MyBudget.Engines.Investments`):** `Portfolio.Analyze` matches sells to lots FIFO,
  classifies each slice short (held ≤ 1 year) or long, then applies the wash-sale rule: a loss is
  disallowed in proportion to same-holding shares bought within 30 days either side (DRIP
  reinvests count), and the disallowed amount is added to the replacement lot's basis; open
  windows are reported with the earliest safe repurchase date (INV-5, ALT-5). `Portfolio.Reinvest`
  builds the DRIP trade (INV-2a). `GainsTax.Estimate` taxes short-term at the ordinary marginal
  rate, stacks long-term on ordinary income across 0/15/20% using the year's thresholds, and
  Missouri at its top rate (INV-4).
- **Sync (`InvestmentService`):** fetch closes + dividend events, store new prices, post dividends
  from shares held on the ex-date, create reinvest trades for DRIP holdings at the close on the pay
  date (or ex-date). Manual dividends and prices go through the same paths.
- **Net worth:** a brokerage account with holdings is valued from shares × latest price instead
  of its typed snapshot; assets add their latest value.
- **Ordinary-income context** for the tax estimate comes from the W-2 paycheck year estimate and
  the filing status on the W-4 (`OrdinaryContextAsync`), reused by the 1099 estimate.

- **Tax-lot import:** a brokerage "tax lots" export (J.P. Morgan layout: Ticker, Quantity, Unit Cost,
  Acquisition Date, Price, Pricing Date) becomes one holding per ticker and one Buy per lot in the
  chosen account; cash and money-market rows are skipped; lots already present (same date, shares,
  cost) are left alone; the day's price is recorded.
- **Tax-advantaged accounts** (HSA, 401(k), Traditional IRA, Roth IRA) are excluded from realized
  gains and the tax estimate; wash-sale warnings still show.

## Open Questions

- Net-worth history values holdings at the latest price for every point (no price-history walk).
- Loss carryforward and specific-lot identification are not modelled; FIFO only.

## Status

Built 2026-09-26: engine (7 tests), API (2 endpoint tests), Investments page, assets tab on Reports.
