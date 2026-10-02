# Investments

**What it answers:** what do I hold, what did it cost, and what has it paid?

A tree with three levels: **account → holding → tax lots**. Expand an account to see its holdings,
expand a holding to see the individual lots.

| Column | Meaning |
| --- | --- |
| Shares, Price | What you hold and the latest price |
| Day | Change since the previous close, in dollars and percent |
| Value, Cost | Market value and what you paid |
| Unrealized | The difference, in dollars and percent |
| Dividends | Paid to you this year |
| Est. / yr | What a full year at the current rate would pay |
| DRIP | Whether dividends are reinvested |

Hover a ticker for the security's full name.

## Value over time

Above the tree, a chart of market value with cost basis beneath it, over the last 1M, 3M, 6M, 1Y
or 3Y. The line under the heading splits the change across the window into the market (the
movement in unrealized gain) and money in or out (the movement in cost: buys, reinvested dividends,
sells). The last point is today's Market value tile.

## Getting data in

Import a brokerage export on [Import](import.md) — a tax-lot CSV or an investment OFX/QFX. It creates
the holdings, the lots, and records the price. You can also add a holding and enter trades by hand.

Prices and dividends are fetched from a free market source. **Refresh prices & dividends** updates
everything; the result is one line saying what changed, with detail behind a disclosure.

## Holdings that have no market price

Two kinds, both marked so you know why there is no daily change:

- **Cash and money-market funds** hold at $1.00. These are a *balance*, not a position: set it with
  **Cash balance** on the holding and the app records the difference. Never add a second lot for cash
  — every statement restates the same pot.
- **Plan-only funds** — a 401(k) collective trust like `VGI001480` — are real positions with a real
  NAV, but no market quotes them. They are marked **plan**, nothing tries to look them up, and their
  price comes from the statement. Re-import a fresh statement, or type the NAV under **Price**.

## Tax lots

Newest first. Each lot keeps its own acquisition date and cost, which is what makes short-term and
long-term gains correct and lets the app check for wash sales when you sell.

## Fees

Plan fees taken out of a holding are recorded with a running total for the year — the one cost that
comes straight out of your balance without an invoice.
