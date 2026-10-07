# Budget

**What it answers:** what do I plan to spend, and what did I actually spend?

A grid: one row per budget line, one column per month. Cells show what you expect; where an actual
has been recorded it shows that instead, in red if it went over.

## What a budget line is

Anything you plan to spend money on. Not only bills — if you mean to set aside $300 a month for fuel,
that is a budget line too.

Each line says:

- **Name, Category, Label** — what it is. The label is optional; use it to budget one merchant apart
  from the rest of its category. See [Categories & Labels](categories-and-labels.md).
- **Usual amount and Frequency** — how much, how often.
- **How it's paid** — bank account, credit card or cash, and **Funded from**: the account the money
  really comes from. That last one drives the transfer figures on [Accounts](accounts.md).

## Frequencies

| Frequency | Use it for | How it is budgeted |
| --- | --- | --- |
| Monthly | Rent, phone, a subscription | The amount, every month |
| Quarterly / Semi-annual / Annual | Insurance, memberships, an annual fee | Divided across the months so it is saved up before it lands |
| One-off | A single known future cost | Spread over the months until its due date |
| **Variable** | Groceries, fuel, dining — an allowance | The amount, every month, as a ceiling |

**Variable is the one people get wrong.** It is not a bill and has no due date. It means "up to this
much a month is fine". A month under the amount is under budget, not a problem — the cell only turns
red when you go over.

For non-monthly lines the app saves up rather than hitting you in one month: a $1,200 annual bill
budgets $100 a month. Hover the cell to see it — `$1,200.00 × 1/yr ÷ 12`.

## When an amount changes

A gym goes from $50 to $51 in October. **Do not edit the usual amount** — that would restate the nine
months you already budgeted and reconciled at $50.

Use **Amount changes** in the editor:

```
Usual amount   50.00

Amount changes
  2026-10   51.00   membership went up
```

January to September stay at $50; October onward is $51. Add another row when it changes again.

## A single odd month

A double charge, a credit, a month you skipped: click the cell and set **Expected amount** for that
month only. That is an override, not a price change, and it beats the schedule. The cell gets a small
dot so you can see it was set by hand.

The same editor records what you **actually** paid and when. A statement import fills this in for
you — see [Import](import.md). On a month not yet paid, **Paid on** starts at today, since opening the
month usually means you're paying it now. The date is saved only once you enter **Amount paid**.
Changing a note or the expected amount doesn't mark the month paid. Variable lines such as Fuel start
with no date, because they add up over the month.

## Sorting and filtering

- **Sort** by name, or by category then name.
- **Category** filters to one category.
- **Columns** hides months you don't care about.
- **Show inactive** brings back retired lines.

## Retiring a line

Untick **Active** rather than deleting, and the history stays. Delete only removes something entered
by mistake.
