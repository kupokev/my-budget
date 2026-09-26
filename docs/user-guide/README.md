# User guide

How to actually use the app day to day — importing a statement, logging a trade, marking an
account as rainy-day fund, reading the rewards progress view, etc. Written for the single user of
this app (not a general audience), so it can assume familiarity with the domain (HSA, DRIP,
IHG/Hilton status) and focus on *this app's* specific workflow for each.

## Phase 1 basics

- **Income:** add each employer/source. Salary history is one row per rate with its effective date.
  Pay schedules are also effective-dated: to model the 2026 switch, keep the semi-monthly row and add
  a bi-weekly row effective the first day of the new cadence with one known pay date as the anchor.
  The pay-date calendar on the right shows every check and highlights three-check months.
- **Accounts:** one row per place money sits. "Funded from" on bills drives the transfer-needs
  panel at the bottom: expand an account to see each bill's monthly accrual and its formula.
  Use "Balance / transfer" to record a balance snapshot or a transfer into the account; Long/Short
  compares transfers this month to the monthly need.
- **Bills:** the year grid is the old bill tab. Click a month cell to open that month's editor:
  type the actual and press Enter for the quick case, or also set this month's due date or expected
  amount when they differ from the bill's defaults (a blank field means "use the default"). A blue
  dot on a cell marks a month with an override; hover a cell for its details. Grey parentheses are
  expected amounts for months the bill is due; red actuals are over. A bill that stops (a payment
  plan ending in October) gets an End date on the bill, which zeroes later months automatically.
  Category, Due, Paid via and Funded from are hidden by default: use the Columns menu, or hover the
  bill name. "Paid via" is the account or card that pays; "Funded from" is where the money really
  comes from (for a card-paid bill, the account that covers it).
- **Cards:** the summary shows which bills sit on each card, their monthly total, the latest
  balance and utilization. "Balance" records a statement or month-end balance.
- **Home:** bills due in the next 14 days, the next pay date, and per-account transfer needs.
