# Getting started

## What this app is

A personal budget app that replaces a spreadsheet. It tracks bills, accounts, credit cards and debts
like any budget app, and adds four things a spreadsheet does badly: a **paycheck estimator** with
real federal and Missouri withholding, an **HSA planner**, a **credit-card rewards optimiser**, and
**investment tracking** with tax lots.

It runs entirely on your own machine. There is no server, no account to create, and nothing is sent
anywhere. Your budget is one file:

```
~/.local/share/MyBudget/mybudget.db
```

## A fresh install

The app starts empty. There is no sample data — nothing invented that you then have to find and
delete. You do get four categories to begin with (Fees, Utilities, General Merchandise, Taxes) and a
"Credit Card" label, because almost everyone needs those. Delete them if you don't.

Tax tables and HSA contribution limits are loaded, because those are facts rather than your data.

## The order that works

You can do these in any order, but each one makes the next easier.

1. **[Accounts](accounts.md)** — every place money sits: checking, savings, brokerage, HSA. You need
   at least one before you can add a bill, because a bill has to say where the money comes from.
2. **[Categories & Labels](categories-and-labels.md)** — the buckets you think in. Don't overthink
   it; you can add more whenever something doesn't fit.
3. **[Income sources](income-sources.md)** — your employer, your salary, how often you are paid. This
   drives the paycheck estimate and the per-paycheck transfer figures.
4. **[Budget](budget.md)** — one line per thing you plan to spend money on. Bills with a due date,
   and allowances like groceries that vary.
5. **[Cards](cards.md)** and **[Loans](loans.md)** — what you owe and what it costs.
6. **[Import](import.md)** — your first statement. Everything above makes this step mostly automatic.

## Starting over

Close the app and delete `~/.local/share/MyBudget/mybudget.db`. Take a backup first from
**[Settings](settings.md)** if there is any chance you want it back.

## Backing up

**Settings → Export budget** writes the whole thing to a single file. Do this before upgrading, and
before any large import. **Import budget** puts it back. See [Settings](settings.md).
