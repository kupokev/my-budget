# DD-0009: Income and pay allocation

> **Status:** Draft — the versioning design below is not built
> **Last updated:** 2026-09-29
> **Related ADRs:** —

## Context / Problem

Income started as a header on a spreadsheet: a salary and a pay frequency. It is now the largest
effective-dated subsystem in the app — salary history, pay schedules, deductions, W-4s, per-cheque
overrides — and it had outgrown its half of DD-0002 and the amendment in DD-0003. This document takes
it over; those two keep the pay-date engine and the withholding formulas.

Everything about a cheque is effective-dated **except the one thing that changes most often: where the
money goes.**

The concrete case: a new loan starts, and the allocation has to shift — more into the account that
autopays it, less into savings. That is not a correction of the old allocation. It is a new arrangement
from a date, with the old one still true for every cheque before it. Overwriting it:

- makes every past cheque's *Where it lands* comparison wrong, which is the whole reason the deposits
  are tagged in the first place;
- leaves "why has savings been $200/month lighter since March?" unanswerable from the app;
- silently rewrites history in a file whose data is real.

## Design

### What income already holds

| What | Dated by | A change is |
| --- | --- | --- |
| `SalaryRate` | `EffectiveDate` | a new row (a raise) |
| `PaySchedule` | `EffectiveDate` | a new row (frequency, or live → arrears) |
| `DeductionElection` | `EffectiveDate` + `EndDate` | a new row, or an end date |
| `WithholdingElection` | `EffectiveDate`, unique per source | a new row (a new W-4) |
| `PaycheckOverride` | `PayDate`, unique per source | one odd cheque |
| `DepositSplit` | **nothing** | destructive |

The lookup rule is the same everywhere: *the latest row whose `EffectiveDate` ≤ the pay date*. It is
already written and tested in `PaySchedules`/`SalaryRates`. Allocation should use that rule, not a
second one.

### Version the set, not the row

The obvious move — put `FromDate`/`ToDate` on `DepositSplit` — is wrong here, and it is worth writing
down why, because it is the pattern used everywhere else in the app.

A reallocation is **one coordinated change across several rows.** You do not lower savings by $200; you
lower savings by $200 *and* raise the autopay account by $200, in the same breath, because the cheque
still has to add up. The invariants live on the set, not the row:

- exactly one row takes the remainder;
- the fixed amounts have to leave something for it;
- the order the fixed amounts come off in is a property of the whole list.

Per-row dating cannot express those. It lets you save a Tuesday on which two rows are the remainder and
a Wednesday on which none is, and nothing in the model says that is impossible. It also turns "what was
the allocation in March?" from one lookup into a date-range overlap across rows, which is exactly the
kind of query that quietly returns the wrong thing.

So the allocation is versioned as a whole:

```
DepositAllocation                      -- one arrangement, in force from a date
  Id
  IncomeSourceId
  EffectiveDate                        -- unique per source
  Reason?                              -- "started the truck loan"
  Notes?
  Lines : List<DepositAllocationLine>

DepositAllocationLine
  Id
  DepositAllocationId
  AccountId
  Amount?                              -- null when IsRemainder
  IsRemainder                          -- exactly one per version
  Order                                -- contiguous from 0, assigned server-side
  Notes?
```

`IncomeSource.AllocationOn(DateOnly)` returns the version in effect; `SplitOf(net, asOf)` moves onto it.
The splitting arithmetic itself does not change — fixed amounts come off in `Order`, each capped at what
is left, and the remainder row takes the balance — only which set of lines it runs against.

**No `IsActive` on a line.** A line you have stopped is simply absent from the next version; that is
what stopping means once versions are dated. Keeping a flag as well gives two ways to say one thing,
and sooner or later they disagree.

### Invariants, enforced in the API

Not in the UI. The UI makes the right thing easy; the API makes the wrong thing impossible.

- Exactly one `IsRemainder` line per version. A version with no lines is allowed — it means "no split
  recorded" — but a version with lines and no remainder is rejected, because it describes a cheque that
  does not all arrive somewhere.
- One version per `(IncomeSourceId, EffectiveDate)`, by unique index, as `WithholdingElection` already
  does.
- `Order` is assigned server-side from list position. It is not a number the UI can get wrong.
- `AccountId` must exist; `Account` keeps its restrict-on-delete, so an account named in any version
  cannot be deleted out from under history.

### Editing

A versions list, newest first, exactly like salary history — and the button is **"Reallocate from
<date>"**, not "Add". It copies the current version's lines into a new one and opens it, because a
reallocation is nearly always the same accounts with different numbers, and retyping four accounts to
move $200 is how mistakes get made.

Editing a past version stays allowed: a typo in history is still a typo. Deleting a version is allowed;
deleting the last one returns the source to having no split, which is the state every source starts in.

### Why `Reason` is a field and not a note in his head

In his words: *"I may start paying on a new loan and need to adjust my pay allocation so that more money
goes into the autopay accounts instead of savings."* Six months later the question is "why did savings
drop in March?" and the answer has to be in the app, next to the change, or it is gone. It shows in the
version list and as the caption on the Paycheck screen's *Where it lands* table.

This is the auditability principle applied to a decision rather than a calculation.

### The check that makes versioning worth building

`TransferNeeds.Compute` (DD-0002) already knows what each funding account needs per paycheck: the sum of
its lines' monthly accruals, × 12 ÷ cheques per year, each with its formula. The allocation says what
each account actually **gets** per cheque.

Comparing them answers the question he is really asking when he opens the editor:

| Account | Allocated / cheque | Needed / cheque | Short or over |
| --- | --- | --- | --- |
| Autopay | 900.00 | 1,012.50 | **−112.50** |
| Savings | 400.00 | — | +400.00 |

It belongs in two places: under *Where it lands* on Paycheck, and **live in the allocation editor while
he is typing the numbers** — that is the moment it is worth anything, because he is changing them
*because* a loan landed.

One honest caveat to show with it: the remainder account's figure depends on the net, so it moves with a
raise or a deduction change. The check is "as of this cheque", not a promise about the year.

### Historic accuracy, which is the point

`Transaction.IncomeSourceId` and the ±4-day matching window are already built. With versions, the
*expected* side of that comparison comes from the version in force on that pay date — so a cheque from
February is compared against February's allocation, not today's. Without versioning the comparison
silently degrades into noise the first time he reallocates.

### Migrating what already shipped

`DepositSplit` shipped in the `DepositSplits` migration (2026-09-30) and may already hold rows in
`~/.local/share/MyBudget/mybudget.db`. This is a **data** migration, not just a schema one:

1. For each source with splits, create one `DepositAllocation` with `Reason = "imported from the
   original split"` and `EffectiveDate` = that source's earliest `SalaryRate.EffectiveDate`, falling
   back to today when it has none.
2. Copy each `DepositSplit` (where `IsActive`) into a line, preserving `Order`.
3. Drop `DepositSplit`.

Backdating to the earliest salary date rather than today matters: it means every cheque the app can
estimate has an allocation behind it, instead of a wall before which *Where it lands* is blank.

### Deliberately not doing

- **Per-cheque allocation overrides**, the way `PaycheckOverride` works for gross. Payroll does not work
  that way, and if a one-off ever happens the actuals matching already shows it as a difference. A
  second override mechanism would earn its keep only when there is a real cheque it explains.
- **Percentage lines.** Payroll takes dollars. A percentage would be a second way to say the same thing,
  and the two would disagree on rounding. If his payroll ever offers percentages it becomes a third line
  kind, and the invariant list grows with it — not before.
- **Moving money.** This records what payroll does. It never initiates a transfer.

## Open Questions

- Where does a shortfall alert belong? Home is a financial-health snapshot, and per the same reasoning
  that keeps rewards alerts on Cards, "your allocation is $112 light" probably belongs on Accounts or
  Paycheck rather than Home.
- Should the check warn when a version's fixed amounts could exceed a *typical* net, not just the cheque
  being viewed? That needs a definition of typical, which is a year estimate — cheap to add, easy to get
  subtly wrong.
- 1099 sources arrive as one payment and need no split. Nothing should break: a source with no versions
  returns no `Deposits` and the Paycheck panel shows its pointer instead. Worth a test rather than an
  assumption.

## Status

**Shipped (2026-09-29/30)** — the flat, undated version:

- `IncomeSource.DepositSplits` and `SplitOf(net)`; `DepositSplit` entity
- Income sources editor: *Where the deposit lands*, ordering, one-remainder enforcement on save
- `PaycheckEstimateDto.Deposits` with expected shares and matched actuals (`WithActualsAsync`, ±4 days)
- Paycheck screen *Where it lands* table, including "not imported" rather than a false shortfall
- `Transaction.IncomeSourceId`; `CategoryRule.IncomeSourceId`; payroll-wording detection and the
  spread-to-merchant offer in import

**Pending** — everything in this document: `DepositAllocation`/`DepositAllocationLine`, the data
migration off `DepositSplit`, `AllocationOn`/`SplitOf(net, asOf)`, the versions editor with "Reallocate
from", and the allocation-vs-transfer-needs check.

## References

- DD-0002 — pay dates (`PayDates`), funding accounts, `TransferNeeds.Compute`
- DD-0003 — withholding formulas and the paycheck estimate this allocation divides
- DD-0005 — import, where a deposit gets tagged to an income source
- `src/MyBudget.Domain/IncomeSource.cs`, `src/MyBudget.Api/PaycheckService.cs`
- `docs/user-guide/income-sources.md`, `docs/user-guide/paycheck.md`
