# Transactions

**What it answers:** what actually happened, and is it categorised?

Every transaction, from every account and card, in one place. This is deliberately the only screen
where you enter a transaction by hand — so you never have to remember which screen a particular kind
of spending lives on.

## Filtering

By year and month, account or card, category, or free text. **Uncategorised** is the useful one after
an import: it shows only what still needs a decision.

## Running balance

Pick a single account under **Source** and a **Balance** column shows the account's balance just
after each line. It is worked out the same way as the balance on Accounts, so the newest line matches
it. A statement balance includes everything dated on or before its day. Lines after a statement run
forward from it, and lines on or before it run backwards from it. Hover a balance to see which
statement it starts from. A hand entry reconciled with a bank line shows "—", because the bank line
is the one that counts. Filters hide lines but don't change the balances.

## Categorising

Set the budget line and the category and label follow, because a budget line already knows both. Where
the line has no label of its own, you can pick one from its category — Amazon or Costco under General
Merchandise.

## Pay

On a row with money in, **Pay from** marks it as a payroll deposit from that job. Doing so takes it out
of spending entirely — no category, no label, no budget line — and feeds the **Where it lands** table on
[Paycheck](paycheck.md), which adds up what arrived across every account your cheque splits into.

The list shows such a row as *Pay · <job>* in place of a category.

## Reconciling

A transaction you entered by hand and the same one arriving later in a statement are the same event.
The app spots likely pairs by amount and date and offers to reconcile them, so the figure is not
counted twice. **Unreconciled** filters to what is still outstanding.

## Transfers

Money moving between your own accounts is not spending. Marked as a transfer, it is excluded from
spending figures. Statement imports guess these — "Payment to Chase card ending 9039" — and you can
correct any guess.

## Deleting

Deleting a transaction that came from an import removes only that row. To undo a whole import, use
**Undo** against the batch on [Import](import.md).
