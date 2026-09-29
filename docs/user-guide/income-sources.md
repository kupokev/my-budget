# Income sources

**What it answers:** who pays me, how much, how often, and what comes out before I see it?

One entry per employer or income source. This drives [Paycheck](paycheck.md), the per-paycheck
transfer figures, and the next-payday tile.

## Salary

One row per rate with the date it took effect. A raise is a new row, not an edit — the old rate stays
true for the months it applied to.

## Pay schedules

Also effective-dated, so a change of cadence is a second row rather than an edit.

| Field | Meaning |
| --- | --- |
| Effective | When this schedule starts |
| Frequency | Bi-weekly, semi-monthly, monthly |
| Known pay date | Any real pay date; every other one is derived from it |
| **Pay lag** | Live pay, or one or two weeks in arrears |
| Weekend → Friday | Move a payday landing on a weekend to the Friday before |

**Pay lag** records which stretch of work a cheque is for. *Live* means the cheque runs through
payday itself; *in arrears* means the period closed earlier. It changes what the cheque covers, not
how much it is.

Switching from live to arrears in October? Keep the existing row and add a second one effective from
the change, with the anchor date shifted and the lag set. History stays correct.

## Deductions and withholding

Pre-tax and post-tax deductions, each effective-dated, and your W-4 and Missouri equivalents.

A 401(k) deferral reduces income tax only; a Section 125 deduction (health premiums, payroll HSA)
reduces Social Security and Medicare as well. The paycheck formulas show which is which.

## Odd cheques

One cheque that is not a normal period — the short one at a live-to-arrears switch — is an override:
a percentage of normal gross or an exact figure, and whether fixed deductions shrink with it.

## Ending employment

Set **Employment ended** and nothing is generated after that date. Leave it blank while you are still
employed — the field offers **Set a date** rather than showing a misleading default.
