# Dashboard

**What it answers:** is anything wrong, and what is due soon?

The first screen. Everything on it is a summary of something else, so every panel is a link to where
the detail lives.

## Needs attention

A collapsed bar at the top. It only appears when there is something to say, and it counts what it
found: accounts short of what this month needs, bills due with nothing set aside, a rewards threshold
falling behind pace, an HSA behind its target, a card benefit about to expire unused.

Expand it to see each one with a link to the screen that fixes it.

## The four tiles

- **Net worth** — everything owned less everything owed, and the change since last month. Detail on
  the [Wealth dashboard](wealth-dashboard.md).
- **Spending this month** — what has actually gone out, against the same point last month.
- **Next paycheck** — the next pay date and which employer. From [Income sources](income-sources.md).
- **Rainy-day fund** — how many months of expenses the accounts you marked would cover. Mark an
  account on [Accounts](accounts.md).

## Paid this month vs last

A cumulative line of what the Budget grid records as paid: this month against last month, day by
day. Being below the previous line means you are paying out more slowly than last month, not that
you have paid less overall.

Each amount lands on its **Paid on** date; without one, on the day the line was due that month; a
Variable line with neither (Fuel, Clothing) lands on today, or on the last day of a past month.
Imported transactions don't feed it — only what is entered on the grid.

## Due in the next 14 days

Every budget line falling due, what it costs, and which card or account pays it. A line marked paid
on the Budget grid shows ✓ and its paid date, and is dimmed; **Still to pay** totals the rest. This
is the panel worth checking on a Sunday.

## Goals

Each active goal with its status and a progress bar. The tick on the bar is how much of the *time*
has passed, so a bar behind the tick means behind pace. See [Goals](goals.md).

## What changed this month

A few lines the app writes itself: budget lines over plan (the biggest three, then the rest
summed), what you've paid so far against what was planned, bills making their last payment this
month and what that frees each month after (from the line's **Ends** date), bills starting next
month, HSA pace toward its target, goals behind where they should be, and net worth against last
month. An HSA contributions goal is folded into the HSA line rather than repeated. Hover a line for the working
behind its number; click it to go to the page it comes from. Red is something to look at, green
good news. Rewards aren't here; they're on Cards.

With local AI set up in [Settings](settings.md), **Ask the AI what matters most** adds a short note
above the list. The model is given these lines and nothing else, so it can't see anything you can't,
and it isn't asked to do any arithmetic.
