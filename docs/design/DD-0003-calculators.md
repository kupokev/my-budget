# DD-0003: Calculators (Phase 2)

> **Status:** Active
> **Last updated:** 2026-09-26
> **Related ADRs:** ADR-0005 (reference data lives in the database), ADR-0007

## Context / Problem

Three things the spreadsheet could not do well: estimate a paycheck the way payroll actually
withholds it (INC-5–7, INC-9, INC-9a, INC-12), plan HSA contributions month by month (HSA-1–4), and
project loan payoff (DBT-1). All three are pure engines with their formulas exposed, per the
auditability principle. Done when the paycheck estimate lands within $5 of two real stubs and the
HSA plan reproduces the sheet's $6,732.83.

## Design

### Reference data (year-keyed, editable)

`TaxYear` + `TaxBracket` hold federal standard deductions by W-4 status, federal brackets by status,
FICA rates and thresholds, supplemental rates, Missouri standard deductions by MO W-4 status,
Missouri brackets, and the MO supplemental rate. `ContributionLimits` holds HSA, 401(k) and IRA
limits. Both carry `Source`, `Verified`, `Notes`. `ReferenceSeed` inserts 2025 and 2026 from the
engine's built-in `RuleSets` on every start but never overwrites, so edits made on the Tax tables
page persist. The Missouri 2026 values are provisional (see `docs/reference/paycheck-calculation.md`).

### PaycheckEngine (`MyBudget.Engines.Paycheck`)

Inputs: gross for the period, periods per year, deductions (fixed or % of gross, each with a tax
treatment), W-4 inputs, MO W-4 inputs, the year's rules, YTD FICA wages. Output: every line with
its formula plus an ordered list of steps.

| Step | Rule |
| --- | --- |
| Section 125 deductions | Subtract from gross → FICA wages |
| Pre-tax retirement | Subtract from FICA wages → income-tax wages (federal and Missouri) |
| Federal income tax | Pub 15-T percentage method, Worksheet 1A: annualize, + 4(a) − 4(b), − standard deduction for the status (halved with Step 2 checked, brackets halved too), marginal brackets, − Step 3 credits, ÷ periods, + 4(c). The published "standard" table equals the annual brackets shifted by the standard deduction, so the engine stores brackets and deductions rather than the derived table |
| Social Security | FICA wages × rate, capped by wage base − YTD |
| Medicare | FICA wages × rate + additional rate on the part over the threshold (YTD-aware) |
| Missouri | Annualize income-tax wages, − MO standard deduction for the MO W-4 status, MO rate table, ÷ periods, + MO extra |
| Post-tax deductions | Subtract → net |
| Supplemental (bonus) | Flat: 22% federal (37% on the part over $1M YTD supplemental) + MO flat rate + FICA; no pre-tax deductions. Aggregate: withholding on regular + bonus minus withholding on regular alone |
| Year | `ComputeYear` threads YTD FICA through every pay date; `FederalYearEnd` compares annual liability to withheld for the refund/owed figure |

The API's `PaycheckService` picks the salary, schedule, elections, W-4 and tax year in effect on
each pay date, so a mid-year raise or W-4 change shows up on the right checks. An income source's
`EndDate` stops pay dates and estimates; a `PaycheckOverride` on one pay date sets that check to a
fraction of normal gross (or an exact gross), with fixed deductions prorated or left whole, for
cases like a live-to-arrears payroll switch that pays one week of a two-week period. What-if requests
override any of those inputs for both the single check and the whole year.

### HsaPlanner (`MyBudget.Engines.Hsa`)

Limit = Σ over months (tier limit ÷ 12), catch-up prorated the same way; room = limit − all
contributions (employer, payroll, direct); recommended monthly = remaining to target ÷ months left
to the target date; per paycheck = remaining ÷ W-2 pay dates left. Over-contribution is flagged.
Last-month rule and testing period (HSA-5) are not modelled.

### Amortization (`MyBudget.Engines.Amortization`)

Level payment formula; monthly schedule with interest rounded to cents; extra principal per month
with interest and months saved versus no extra; projection from the latest balance snapshot (or
the original principal); "scheduled balance now" for comparing the statement to the original plan.
A sub-1%-of-payment residual after the final scheduled month is folded into that payment.

### API

`paycheck/estimate`, `paycheck/year`, `paycheck/what-if`, `paycheck/supplemental`; `paychecks` CRUD
+ `{id}/compare`; `reference-years`, `tax-tables/{year}` GET/PUT + `copy-from`, `limits/{year}`;
`hsa/{year}` GET/PUT, contributions, `plan`; `loans` CRUD, balances, `projection?extra=`.

### UI

Paycheck page with tabs: Estimate (check + year side by side), What-if, Year (every check), Bonus,
Actual stubs (enter a stub, compare line by line, within-$5 flag). HSA page: click-to-cycle month
grid, plan with steps, contributions. Loans page: list, editor, balance entry, projection with an
extra-principal try box and the full schedule. Tax tables page: edit everything, mark verified,
copy a year forward. Income page gained benefit elections and W-4 rows.

## Open Questions

- Missouri 2026 bracket edges and top rate need confirming against the 2026 MO withholding formula.
- Employer profit-sharing (INC-11) and 1099 set-aside (INC-8) are not started.
- Whether to model the HSA last-month rule (HSA-5, C).

## Status

Built 2026-09-26: engines (paycheck 8 tests, HSA 3, amortization 6), API (9 endpoint tests), four
pages. Stub verification pending Kevin's real numbers (see `docs/audit/`).

## References

- `docs/reference/paycheck-calculation.md` for the formulas and the figures used
- DD-0002 for pay dates (the paycheck year view reuses `PayDates`)
