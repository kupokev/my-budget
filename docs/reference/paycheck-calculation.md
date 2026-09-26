# Paycheck calculation reference

How `MyBudget.Engines.Paycheck` withholds, and the reference figures it ships with. The database
copy (Tax tables page) is what the app uses; this file documents the starting values and where
they come from.

## Federal income tax (Pub 15-T, percentage method, automated payroll, Worksheet 1A)

1. Annualize: taxable wages for the period × periods per year, + W-4 4(a) other income, − 4(b) deductions.
2. Subtract the standard deduction for the W-4 filing status. With Step 2(c) checked, halve the
   standard deduction and halve every bracket threshold (this is exactly Pub 15-T's "checkbox" table).
3. Apply the marginal brackets to get annual tax.
4. Subtract Step 3 credits (floor at zero).
5. Divide by periods per year; add 4(c) extra withholding.

Why brackets + standard deduction instead of copying the 15-T table: the published "standard
withholding rate schedule" is the annual bracket table shifted up by (standard deduction − the
Step-1 constant of $8,600 / $12,900), after the worksheet subtracts that same constant. Storing the
brackets and the standard deduction gives the same number and is easier to confirm each year.

Taxable wages for federal = gross − Section 125 deductions − traditional 401(k). FICA wages = gross −
Section 125 only.

## FICA

- Social Security: 6.2% of FICA wages until year-to-date FICA wages reach the wage base.
- Medicare: 1.45% of FICA wages, plus 0.9% on the part of year-to-date FICA wages over $200,000
  (withholding threshold is the same for every filing status).

## Missouri

Annualize income-tax wages, subtract the Missouri standard deduction for the MO W-4 status (Missouri
uses the federal standard deduction amounts: single; married-spouse-works = single; married-one-income
= joint; head of household), apply the Missouri rate table, divide by periods, add MO W-4 extra.

Missouri rate table shape: 0% on the first step, then 2.0%, 2.5%, 3.0%, 3.5%, 4.0%, 4.5% on the next
six equal steps, then the top rate on everything above 7 steps.

## Supplemental pay (bonus)

Flat method: 22% federal on supplemental wages up to $1,000,000 for the year, 37% above; Missouri's
flat supplemental rate; FICA as usual. Aggregate method: withholding on (regular + bonus) as one
period's wages minus withholding on the regular check alone.

## Figures shipped (confirm each January on the Tax tables page)

| Item | 2025 | 2026 | Source |
| --- | --- | --- | --- |
| Federal std deduction single / MFJ / HoH | 15,000 / 30,000 / 22,500 | 16,100 / 32,200 / 24,150 | IRS Rev. Proc. 2024-40 / 2025-32 |
| Federal 10/12/22/24/32/35/37% thresholds, single | 0 / 11,925 / 48,475 / 103,350 / 197,300 / 250,525 / 626,350 | 0 / 12,400 / 50,400 / 105,700 / 201,775 / 256,225 / 640,600 | same |
| MFJ | 0 / 23,850 / 96,950 / 206,700 / 394,600 / 501,050 / 751,600 | 0 / 24,800 / 100,800 / 211,400 / 403,550 / 512,450 / 768,700 | same |
| HoH | 0 / 17,000 / 64,850 / 103,350 / 197,300 / 250,525 / 626,350 | 0 / 17,700 / 67,450 / 105,700 / 201,775 / 256,225 / 640,600 | same |
| SS rate / wage base | 6.2% / 176,100 | 6.2% / 184,500 | SSA |
| Medicare / additional / threshold | 1.45% / 0.9% / 200,000 | same | IRS |
| Supplemental flat / high / threshold | 22% / 37% / 1,000,000 | same | Pub 15 |
| MO std deduction | = federal | = federal | MO DOR |
| MO bracket step / top rate | 1,313 / 4.7% | **1,313 / 4.7% (provisional, carried from 2025)** | MO DOR withholding formula |
| MO supplemental rate | 4.7% | 4.7% (provisional) | MO DOR |
| HSA self / family / catch-up | 4,300 / 8,550 / 1,000 | 4,400 / 8,750 / 1,000 | IRS Rev. Proc. 2024-25 / 2025-19 |
| 401(k) employee / catch-up / total | 23,500 / 7,500 / 70,000 | 24,500 / 8,000 / 72,000 | IRS Notice 2024-80 / 2025-67 |
| IRA / catch-up | 7,000 / 1,000 | 7,500 / 1,100 | same |

**Provisional** means the value was carried forward and must be checked against the 2026 Missouri
Employer's Tax Guide before the 2026 tables are marked Verified.
