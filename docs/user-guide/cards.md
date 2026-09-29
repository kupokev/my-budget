# Cards

**What it answers:** is each credit card worth what it costs?

One row per card, worst first. Everything that used to be two tables is one, because "what does this
cost" and "what does it give me" are the same question.

| Column | Meaning |
| --- | --- |
| Yearly cost | The annual fee plus interest if the current balance were carried all year |
| Rewards YTD | Points earned this year, valued at your cents-per-point |
| Thresholds | Spend thresholds you have reached |
| Perks | Benefits you have actually used — see below |
| **Net** | Value less cost. Green is earning its keep; red is costing you |
| Balance, Util., Stmt/due | The practical facts |

Hover the card name for what it is paid by, when the balance was recorded, the fee, the interest, and
what is charged to it. Hover **Net** for the full formula.

## Short names

Issuers write card names like `WELLS FARGO CASH WISE VISA PLATINUM® CARD`. Put something short in
**Short name** on the card and every list uses that instead, with the full name on hover.

## The two editors

Each row has two separate editors, because they are different things:

- **★ Rewards** — what *spending* on the card earns: earn rates per category and spend thresholds.
- **🎁 Benefits** — what the card *gives* you: bags, credits, reward nights.

### Benefits, and why you log them

Most card benefits are worth nothing until you use them. A $50 quarterly credit you forget is a fee
you paid for nothing. So a benefit is recorded as what **one use** is worth, and only counts once you
log it.

```
Benefit             Each time used   Resets        Max
Checked bag, RT     90               each year     —
Quarterly credit    50               each quarter  1
Reward night        120              each year     —
```

Log a use with the date and a note ("STL–ATL round trip"). Both are editable afterwards — correcting
a note should not mean deleting the record of a trip you took.

**Resets = each quarter with Max 1** is the important combination: two stays in the same quarter
still release only that quarter's credit, and a quarter you miss does not roll over. The app warns
you in the rewards heads-up when a periodic benefit is unused and time is running out.

**Automatic / yr** is the rarer kind that arrives whether you act or not, like a Global Entry credit.
Leave it at zero for anything you have to use.

## When a fee changes

Cards waive the first year and raise the fee later. Editing the single **Annual fee** would rewrite
what the card cost you in a year that already happened, so use **Fee by year**:

```
From year   Fee that year on   Why
2026        0                  waived, first year
2027        150
```

Years before the earliest row fall back to the plain annual fee, so a card whose fee has never
changed needs nothing here.

## Budgeting the fee

Tick **Budget this fee** and the app keeps a budget line for it, named after the card, filed under
Fees · Credit Card, due in the month the fee posts. Untick it and the line goes away. Delete the line
from [Budget](budget.md) and the tick clears itself, so the two can never disagree.

A card with no paying account cannot budget its fee — a budget line has to say where the money comes
from.
