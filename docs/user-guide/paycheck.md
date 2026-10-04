# Paycheck

**What it answers:** what will my next cheque be, after tax?

Pick a date and it estimates that cheque in full: gross, every pre-tax deduction, federal
withholding, Social Security, Medicare, Missouri withholding, post-tax deductions, and net.

Everything it uses comes from [Income sources](income-sources.md) and [Tax tables](tax-tables.md).

## Reading it

Each line shows its formula. Federal withholding is the percentage-method calculation, not a flat
guess, so it should match your real stub closely. Where it cannot be exact it says so in a warning
rather than quietly rounding.

Under the estimate, the year view lists every pay date with gross and net, so you can see three-cheque
months and what the year totals to.

## What the cheque is for

Under the date it says which stretch of work the cheque covers:

> Covers Sep 19 – Oct 2 · live pay (through payday)

- **Live pay** means the cheque runs to payday itself, so its last days are paid before they are
  worked.
- **In arrears** means the period closed earlier — a week in arrears closes seven days before payday.

Set this per pay schedule on [Income sources](income-sources.md). It does not change how much you are
paid, only which days the cheque is for.

## Where it lands

If the income source has a deposit split, the estimate is followed by a table of the accounts the
cheque divides into — what each should get, and what actually arrived.

| Column | What it means |
| --- | --- |
| Expected | That account's share of this cheque's net |
| Received | Total of the deposits tagged to this job near this pay date |
| Difference | Received − expected; red past a dollar |
| How | The fixed amount, or "balance of … after the fixed amounts" |

Because the money arrives in several accounts, a deposit cannot be matched one-to-one against a
cheque. What is compared is each account's tagged total against its expected share, within four days
either side of payday.

An account with nothing tagged reads **not imported** — not a shortfall. That is usually the truth:
you have imported one bank's statement and not the others yet. Once all of them are in, the totals
should agree; a real difference then means the cheque itself was not what was estimated.

Deposits get tagged during [Import](import.md), or on any row from
[Transactions](transactions.md) with **Pay from**. Set the split up under
[Income sources](income-sources.md).

## Odd cheques

A single cheque that is not a normal period — the short one when an employer switches from live pay
to arrears — is an **override** on the income source. Give it a percentage of normal gross or an
exact figure, and say whether fixed deductions shrink with it.

## Paid time off on a stub

When you enter a stub, there's a row for each time-off bucket on that job: type the hours **Accrued**,
**Used** and the **Balance** exactly as printed. Leave the balance blank for a bucket that stub doesn't
show. The newest balance is where the app projects from, and the stubs table lists each stub's
balances. Buckets are set up on [Income sources](income-sources.md).

## If it does not match your stub

That is a bug, not a rounding difference. The most common causes are a deduction missing from the
income source, a salary rate whose effective date is wrong, or a tax year not yet verified — the
estimate says so at the top when the tables for that year have not been checked against the real
figures.
