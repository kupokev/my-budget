# Import

**What it answers:** how do I get months of real transactions in without typing them?

One upload box for everything. Pick where the file came from, choose the file, and the right importer
runs — **the file decides which**, not you.

| File | What happens |
| --- | --- |
| Bank or card statement (CSV, OFX, QFX) | Transactions, with categories suggested |
| Brokerage tax-lot export (CSV) | Holdings and lots on [Investments](investments.md) |
| Investment OFX/QFX — a 401(k) download | Purchases, fees and the current position |

Supported statement layouts include Chase (checking and card), Capital One, Amex, Discover, Citi,
PNC, Wells Fargo, Wealthfront and a generic Date/Description/Amount fallback.

## The preview

Nothing is written until you press Import. The preview shows every row with what it guessed:

- **Budget line** leads. Choosing one fills in the category, and the label too when the line has one.
- **Category** and **Label** only appear as dropdowns when there is a real choice — where the budget
  line already settles them, they are shown as text so a row cannot disagree with its own line.
- **Transfer** marks money moving between your own accounts, excluded from spending.
- **Skip** leaves a row out.

Hover the merchant to see why something was pre-filled.

Rows already imported are marked as duplicates and are not imported again. Matching is on the bank's
own transaction id where there is one.

## Pay coming in

Money in is not spending, and a payroll deposit is not a transfer either. Rows with a positive amount
get an **Income** column: pick the job and the row becomes pay, which clears the budget line, category
and label — a deposit has none of those.

Wording payroll uses ("PAYROLL", "DIRECT DEP", and similar) is recognised, so the column usually
arrives already filled in. With more than one job on file and nothing in the description to tell them
apart, it is left for you to pick rather than guessed at.

Tagging one deposit offers to do the rest from the same merchant, and to remember it, so next month's
statement arrives already marked. Ticking **Remember for future imports** writes the rule.

What this buys you is the **Where it lands** table on [Paycheck](paycheck.md): once the deposits are
tagged, it can add up what actually arrived across every account the cheque splits into and compare it
to the estimate.

## Doing a merchant once

Categorise one row and a bar appears **under that row**: *"Costco Gas → Fuel. Apply to the other 14
rows from this merchant?"*

**Apply** sets them all. **Remember for future imports** — ticked by default — saves a rule so the
next statement arrives already categorised. That is the setting that turns a 281-row import into a
handful of decisions.

## Undo

Every import is recorded under **Previous imports** with its file, range and counts. **Undo** removes
that batch's transactions and re-syncs the months it touched.

## Investment imports

A brokerage export is reconciled against the position the statement reports. Plan fees have no share
count, so the purchases alone always overstate what is held; the app records the difference as a
dated adjustment so the holding matches the statement and the correction is visible.

Cash and money-market rows are imported too — they are real money — but as a balance rather than a
lot. See [Investments](investments.md).
