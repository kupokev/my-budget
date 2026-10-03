# Changelog

What changed in each release of MyBudget. The newest version is at the top. The release workflow
copies a version's section onto its GitHub release, so write each entry for someone deciding whether
to upgrade.

Changes that haven't shipped yet go under the next version, marked *unreleased*. When you tag it,
replace *unreleased* with the date.

## [1.0.1] — unreleased

### Added

- **Investments value chart.** Market value against cost basis over the last 1 month, 3 months,
  6 months, 1 year or 3 years. Each point is rebuilt from your trades and daily prices, so the last
  point always equals the Market value tile. The line under the heading splits the change into
  market movement and money added or withdrawn.
- **Card terms by year.** The Rewards and Benefits editors show one year at a time. **Change from
  2027** ends the current rows at 2026 and copies them to start in 2027, so next year's terms can
  be entered without touching this year's.
- **Paid status on "Due in the next 14 days".** A bill marked paid on the Budget grid shows ✓ and
  its paid date, and its row is dimmed. A **Still to pay** total sits under the table.
- **Confirmation numbers.** When recording a payment on the Budget grid, there's a field for the
  biller's confirmation number. It also shows when you hover over that month's cell.
- **Budget line sorting.** The Budget page opens sorted by the day of the month each line is due,
  with lines that have no due date last. Sorting by name, or by category then name, is still in
  the Sort list.
- **Price changes on a budget line.** When a subscription goes up, record the new amount from the
  month it starts. Earlier months keep what they were budgeted at.
- **Label lines under a budget line.** Expand a line to see where its money went by label, for
  example General Merchandise split into Amazon, Costco and the rest.
- **Annual fee as a budget line.** Created from the card editor, so the fee is planned for in the
  month it's charged.
- **Paycheck deposit splits.** One paycheck can be split across several accounts, and imported
  deposits are matched to those splits. This is a first version and still needs work.
- **Pay schedules paid in arrears.** A pay schedule can say how many days after the work period
  ends it is paid, so the paycheck is assigned to the right period.
- **1099 income sources** need no salary or pay schedule. Choosing *Contract 1099* hides the
  paycheck settings and shows **Payments received**, where each payment is logged as it arrives
  and can be corrected in place. Tax set-aside and estimated payments stay on Paycheck → 1099.
- **Cash** as a payment method for budget lines.
- **Imports:** QFX files, Wealthfront statements, and investment statements and tax-lot exports
  from the same upload box on Import.
- **Correcting holdings.** Set a cash or money-market balance by hand when no recent statement
  exists.

### Changed

- **Due column on the Budget page.** It now shows just the day of the month a bill is due
  ("7th"). The full schedule, such as "Annual from Jun 1", moved to the hover box.
- **The dashboard spending chart** is now **Paid this month vs last**. It adds up what you've
  marked paid on the Budget grid, not imported transactions. Each amount lands on its paid date,
  or on its due date if it has no paid date. A month marked paid with a date but no amount counts
  at its expected amount, and a paid date later than today isn't counted until that day.
- **Balance alerts** no longer count bills already marked paid, since that money has already left
  the account.
- **Net worth chart** on the Wealth dashboard is about half as tall.
- **"This month in plain English" is now "What changed this month".** The app writes it itself:
  budget lines over plan, paid so far against planned, bills making their last payment (and what
  that frees each month) or starting next month, HSA pace, whether money freed next month covers
  what the HSA still needs, goals that are behind, and the net-worth change. Hover a line for its working. **Ask the AI what matters most** adds a short
  note written from those lines only. Before, the model fetched raw data itself; that overflowed
  its context window, so it ignored its instructions and summarised only the last thing it saw.
  The note is always plain prose, even when the model replies in Markdown.
- **Price changes in the budget line editor** sit on one line each: from month, amount, reason and
  remove.
- **Credit card screens:** the card editor, rewards and benefits were reorganised. Benefits are
  now counted only once you log a use.
- **Category and label pickers.** The label list narrows to the chosen category.

### Fixed

- Opening a budget line's editor showed its label as blank, and saving the line then cleared the
  label.
- A budget line's price changes never appeared in its editor, so they couldn't be corrected or
  removed. The card cost summary and the rewards report also ignored them and used the old amount.
- In the card Rewards and Benefits editors, setting a row's From or Until year past the year on
  screen made it vanish at once, so the Save click that followed missed. The row now stays put
  with a note, and after saving, the editor says which year it went to.
- Editors such as card Benefits could close while you were using them, losing unsaved changes, when
  a mouse press inside the dialog ended over the dimmed background. Only a click that starts and
  ends on the background closes one now.
- The "marked paid in the last seven days" line under the dashboard chart ignored days from the
  previous month early in a month.
- The Arch package step of the release build failed, which stopped the first attempt at 1.0.1
  from publishing.
- Investment accounts showed $0 in goals (including the rainy-day fund) and in the accounts list.
  They're now valued from what they hold.
- 401(k) contributions didn't count toward goals, because the money arrives as fund purchases
  rather than deposits.
- Loan balances disagreed between the Wealth dashboard, the loans list and the net-worth report.
- Variable budget lines showed every month as over budget.
- Several problems importing investment statements, including cash sweep accounts and funds with
  no market price.
- The taskbar icon was missing.
- Hover boxes were clipped, date fields showed misleading defaults, and the AI settings message
  was wrong.

## [1.0.0] — 2026-09-27

First release: Linux desktop packages (AppImage, deb, rpm and Arch) for the budget, cards,
paycheck estimator, HSA planner, rewards optimizer, investments and wealth dashboard, with
optional local-AI summaries.

[1.0.1]: https://github.com/kupokev/my-budget/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/kupokev/my-budget/releases/tag/v1.0.0
