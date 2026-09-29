# Wealth dashboard

**What it answers:** what am I worth, and where does it sit?

## The tiles

- **Net worth** — everything owned less everything owed, with the change since last month and since
  the start of the year.
- **Investments** — market value of every holding, and unrealised gain.
- **Assets** — home and vehicles, with equity after what is owed against them.
- **What you owe** — cards and loans.

## Net worth over time

Three lines: owned, owed, and the difference. Built from what was recorded at the time, walking
backwards month by month — so a balance you record today does not retroactively apply to January.

That is also why a new install shows a flat line that jumps: the history only has data from when you
started entering it. Record historic balances and asset values and the line fills in.

## Where it sits

Every component of net worth on one list: accounts, holdings, assets, and debts as negatives. The
**As of** column matters — it tells you how stale each figure is.

An investment account is valued from what it holds (shares × latest price), not from a typed balance.
A loan with no recorded balance falls back to its original principal and is marked **orig**, so you
can tell an estimate from a real figure.

## Investment year

Market value, cost basis, unrealised gain, dividends this year, and realised gains split into
short-term and long-term — the split that matters at tax time.

## Loans

Balance and rate for each. A balance marked **orig** has never been recorded; record one with the
**＋** on [Loans](loans.md).
