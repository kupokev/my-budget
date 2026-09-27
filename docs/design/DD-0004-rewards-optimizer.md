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
| `LoyaltyTier` | One rung of the ladder: name, rank, and `Benefits` — what holding it actually gets you (breakfast, upgrades, lounge). Shown on Rewards status so the reason for chasing a tier is visible next to the progress toward it |
| `StatusPath` | One way to a tier: CardSpend (card, amount), HoldCard (card), Nights, Stays, ProgramSpend, QualifyingPoints. Optional `StartYear`/`EndYear`. A HoldCard path may point at **any** card, including one from an unrelated brand |
| `LoyaltyProgress` | Year-to-date nights / stays / program spend / qualifying points, entered by hand |
| `EarnRule` | Card × category × **label** → points per dollar. Most specific wins: category+label, then label, then category, then the base rate (both null). Annual cap stored, not enforced |
| `SpendThreshold` | Card calendar-year spend → reward (status tier, free night, credit, bonus points) with a dollar value |
| `CardPerk` | Something a card gives you for holding it, with no spend threshold and no program behind it: a TSA PreCheck credit, a travel credit, free bags. Annual dollar value, optional year range |
| `CardSpend` | Card × month × category × label actual spend. **Derived from transactions on read, never stored** (see below) |
| `Card` additions | Loyalty program, point-value override |
| `Category` additions | `PlannedMonthly` variable spend, `IsCardEligible`. Edited on Admin → Categories & labels only; the rewards report used to carry a duplicate editor for the same two fields and no longer does |
| `Label` | Where a purchase happened (Amazon, Costco, IHG) next to the category that says what kind it was. Optional usual category and planned monthly spend, which is **carved out** of that category's planned amount rather than added to it. Carried on transactions, categorization rules, card spend and earn rules |
| `BudgetLine` additions | `IsCardEligible` (mortgage, HELOC, car: false) |

Cards, programs and their rules are entered by hand: "New card" on the Cards page, then the card's
rewards editor (★) for the program link, point value, earn rules and spend thresholds, and
Admin → Loyalty programs for tiers, status paths and year-to-date activity (programs are set up once and rarely touched, so they live in Admin, not on the Rewards report). A built-in card catalog was
tried and removed (see the decision note below): the data went stale, it only helped once per card,
and it cluttered the page.

**Terms change year to year**, so `EarnRule`, `SpendThreshold`, `StatusPath` and `CardPerk` each
carry an optional `StartYear`/`EndYear`; blank means always. The optimizer only counts what applies
in the year being reported, so a 2026 promo rate leaves 2027 planning on the base rate, a
qualification path a program has since dropped stops appearing, and last year's figures stay right.

**Status crosses program lines two ways.** A card can grant a tier in a program it is not otherwise
tied to — an IHG Premier card makes you Hertz Five Star — which is a HoldCard path on the Hertz
program pointing at the IHG card; nothing links the card to Hertz otherwise. And a tier earned last
year is a tier you hold this year: `LoyaltyProgram.CurrentTier` is what you hold *now*, so setting
it at or above the target marks the target reached for the current year with no spend asked for.
Because status lapses, a report for a future year ignores `CurrentTier` (`RewardsInput.CarryCurrentTier`),
which is why the 2027 plan asks for the $40,000 again even though 2026 needs nothing.

### Points or cash for one booking (`RedemptionCalculator`, RWD-7)

A redemption is judged per booking, not per program. A single "value per point" on the program can't
describe a currency whose worth swings more than two to one with the cash price: measured against real
Atlanta bookings, Hilton ran 0.37¢ at a $219 room and 0.77¢ at a $574 one, while IHG stayed between
0.47¢ and 0.65¢ and actually sagged at the top end. Hilton holds its award prices nearly flat while IHG
prices awards against the cash rate, so the two programs reward opposite booking habits and no single
number per program is honest about either.

So the calculator takes one stay priced both ways and returns cents per point plus a verdict:

| Band | Meaning |
| --- | --- |
| under 0.45¢ | Pay cash; the points are worth more elsewhere |
| 0.45¢ – 0.55¢ | A wash; decide on cancellation terms or availability |
| 0.55¢ and up | Use the points |

Two corrections separate this from dividing one price by the other, and both were derived from real
bookings rather than assumed:

1. **Taxes count.** An award stay pays none, so the all-in cash price is what redeeming avoids. On a
   $179 room with $20 of fees that's the difference between 0.59¢ and 0.65¢.
2. **A cash stay earns points**, and booking with points forfeits them: program base, elite bonus and
   the card's rate. On the same booking this moved 0.65¢ to 0.56¢, most of a band. It is optional in
   the input because it needs an earn rate, but leaving it out biases every answer optimistic.

Inputs are totals for the whole stay rather than per night, so a fourth- or fifth-night-free benefit
is already reflected in whatever the booking page quoted and needs no separate modelling.

### Engine (`MyBudget.Engines.Rewards.RewardsOptimizer`)

Input: year, as-of date, active cards (with rules, thresholds, program), active programs (paths,
tiers, progress), the year's card spend, categories, active bills, and each bill's monthly accrual.

1. **Projected card-eligible spend per month**, keyed by `(category, label)` = card-eligible bills'
   monthly accruals + each card-eligible category's planned variable spend, with each label's planned
   amount moved out of its category's bucket into its own.
2. **Thresholds (RWD-3):** per card threshold: YTD, remaining, remaining ÷ months left, and a
   year-end projection at the YTD pace, with the formula.
3. **Programs and plan (RWD-2/4/6):** programs in priority order. If a HoldCard path grants the
   target tier, the current tier already meets it (carried from an earlier year), or any path is
   already met, the target is reached. Otherwise the CardSpend path's
   required monthly is taken from the remaining projected pool; a shortfall is a **gap** listing
   the other paths' remaining amounts ("32 more nights", "$7,300 more eligible program spend",
   "hold the Aspire").
4. **Card value:** each card's yearly value is points earned × point value + reached threshold
   rewards + perks that apply this year, less the annual fee, so a fee can be judged against
   everything the card actually returns, not just points.
5. **Routing:** goal cards take the categories they earn most on first, up to their allocation;
   what's left goes to the highest earn × point value card per category.
5. **Budget lines (RWD-4/4a):** each card-eligible line gets the card its category is routed to; the bank
   discount wins if it is at least the card value, unless that card is a status goal that is short.
6. **Earnings (RWD-5):** per card per month points = Σ spend × category rate; dollars at the card's
   (or program's) point value; net = value + reached threshold rewards − annual fee.

Months left counts the current month. A future year runs from January 1 with zero YTD (the
"2027 plan by January"); a past year runs as of December 31.

### API and UI

`cards/{id}/rewards` GET/PUT, `loyalty-programs` CRUD,
`card-spend` GET / PUT (upsert by card-month-category, 0 deletes), `categories/{id}/plan`,
`rewards/report?year=&asOf=`.

Rewards page tabs: Progress (programs with every path, thresholds), Plan (gaps, routing, goal
allocations, steps), Budget lines (recommendation per line), Earnings (per card, by month), Programs
(edit priority, tiers, paths, YTD activity), Card spend (month × category grid per card), Planned
spend (per category). Cards page: per-card Rewards editor (★).

### Removed: the built-in card catalog (2026-09-27)

The original plan (requirements doc, "Cards and rewards") was a built-in editable catalog of common
issuer/product combos. It shipped in Phase 3 and was removed: a dozen hard-coded cards' earn rates
and thresholds are guesses that drift, the value lands only the first time a card is added, and the
picker sat in the toolbar forever. Everything it filled in is a few fields in the rewards editor.
Cash back is the same machinery: no program, a point worth 1¢.

## Open Questions

- Earn-rule annual caps and Discover's rotating categories are noted, not enforced.
- A transaction carries one label. A purchase that would want two tags needs the more specific one.
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


## Card spend is derived, not stored (2026-09-27)

`CardSpend` was a table with its own editing grid on the rewards page. Only an import ever refreshed
it, by grouping that card's transactions by month, category and label. So it was a cache of the
transaction table, and a card transaction entered by hand never reached it, which meant it never
reached the spend thresholds either.

The table, its endpoints and its grid are gone. `RewardsEndpoints.CardSpendFor` runs the same
grouping over `Transactions` when the report is built, excluding transfers and counting a reconciled
manual/imported pair once. The figures are now always current, and the seeded year of card spend is
seeded as transactions.

The Cards page gained read-only `SpentThisMonth` and `SpentLastMonth` columns from the same source,
so "what did I put on this card this month" still has a home without a second place to type it.
