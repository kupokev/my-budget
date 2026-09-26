# DD-0004: Rewards and status optimizer (Phase 3)

> **Status:** Active
> **Last updated:** 2026-09-26
> **Related ADRs:** ADR-0001 (no live card API; catalog instead), ADR-0007

## Context / Problem

Two hotel programs each offer Diamond for $40,000 of calendar-year spend on their co-branded card,
which is $80,000 of card spend against a household that puts far less than that on cards. The
optimizer (RWD-1–6, RWD-4a) has to show progress toward every threshold and tier, say how much per
month each needs, allocate the projected card-eligible spend in Kevin's priority order, route the
rest to the best-value card per category, weigh a bill's bank-autopay discount against its card
value, and say plainly when the spend can't cover every goal and what the other paths are.

## Design

### Data

| Entity | Purpose |
| --- | --- |
| `LoyaltyProgram` | Program, point value in cents, points balance, current and target tier, priority (1 first). Tiers ordered by rank |
| `StatusPath` | One way to a tier: CardSpend (card, amount), HoldCard (card), Nights, Stays, ProgramSpend, QualifyingPoints |
| `LoyaltyProgress` | Year-to-date nights / stays / program spend / qualifying points, entered by hand |
| `EarnRule` | Card × category → points per dollar; null category = base rate. Annual cap stored, not enforced |
| `SpendThreshold` | Card calendar-year spend → reward (status tier, free night, credit, bonus points) with a dollar value |
| `CardSpend` | Card × month × category actual spend, from statements until import (Phase 4) fills it |
| `Card` additions | Loyalty program, point-value override, catalog key |
| `Category` additions | `PlannedMonthly` variable spend, `IsCardEligible` |
| `Bill` additions | `IsCardEligible` (mortgage, HELOC, car: false) |

`CardCatalog` is a built-in list of common cards (IHG Premier, Hilton Surpass and Aspire, Sapphire
Preferred, Freedom Unlimited, Double Cash, Quicksilver, SavorOne, Venture, Active Cash, Discover it,
Blue Cash Everyday) with earn rules and thresholds, plus the IHG and Hilton program definitions
with 2026 tiers and non-card paths. "Add from catalog" creates the card, its rules and thresholds,
the program if missing, and the card's status paths (HoldCard tier, CardSpend tier). Everything is
editable afterwards; the catalog values are a starting point to check against current terms.

### Engine (`MyBudget.Engines.Rewards.RewardsOptimizer`)

Input: year, as-of date, active cards (with rules, thresholds, program), active programs (paths,
tiers, progress), the year's card spend, categories, active bills, and each bill's monthly accrual.

1. **Projected card-eligible spend per month by category** = card-eligible bills' monthly accruals
   + each card-eligible category's planned monthly variable spend.
2. **Thresholds (RWD-3):** per card threshold: YTD, remaining, remaining ÷ months left, and a
   year-end projection at the YTD pace, with the formula.
3. **Programs and plan (RWD-2/4/6):** programs in priority order. If a HoldCard path grants the
   target tier, or any path is already met, the target is reached. Otherwise the CardSpend path's
   required monthly is taken from the remaining projected pool; a shortfall is a **gap** listing
   the other paths' remaining amounts ("32 more nights", "$7,300 more eligible program spend",
   "hold the Aspire").
4. **Routing:** goal cards take the categories they earn most on first, up to their allocation;
   what's left goes to the highest earn × point value card per category.
5. **Bills (RWD-4/4a):** each card-eligible bill gets the card its category is routed to; the bank
   discount wins if it is at least the card value, unless that card is a status goal that is short.
6. **Earnings (RWD-5):** per card per month points = Σ spend × category rate; dollars at the card's
   (or program's) point value; net = value + reached threshold rewards − annual fee.

Months left counts the current month. A future year runs from January 1 with zero YTD (the
"2027 plan by January"); a past year runs as of December 31.

### API and UI

`card-catalog`, `cards/from-catalog/{key}`, `cards/{id}/rewards` GET/PUT, `loyalty-programs` CRUD,
`card-spend` GET / PUT (upsert by card-month-category, 0 deletes), `categories/{id}/plan`,
`rewards/report?year=&asOf=`.

Rewards page tabs: Progress (programs with every path, thresholds), Plan (gaps, routing, goal
allocations, steps), Bills (recommendation per bill), Earnings (per card, by month), Programs
(edit priority, tiers, paths, YTD activity), Card spend (month × category grid per card), Planned
spend (per category). Cards page: add from catalog, per-card Rewards editor.

## Open Questions

- Earn-rule annual caps and Discover's rotating categories are noted, not enforced.
- Threshold rewards other than status don't attract allocation on their own (the $15K free night
  is progress-only); worth a "chase this threshold" flag if it matters.
- Once statement import exists, `CardSpend` should be derived from transactions rather than typed.

## Status

Built 2026-09-26: engine (5 tests against the doc's $40K/$40K tension), API (4 tests), Rewards
page, catalog. Seeded card spend and program activity are placeholders.

## References

- Requirements doc, "Rewards and status" calculation rules and sources (Chase IHG, NerdWallet,
  The Points Guy, LoyaltyLobby)
- DD-0002 (bill accruals feed the projected spend)
