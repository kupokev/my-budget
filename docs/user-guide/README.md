# User guide

How to actually use the app day to day — importing a statement, logging a trade, marking an
account as rainy-day fund, reading the rewards progress view, etc. Written for the single user of
this app (not a general audience), so it can assume familiarity with the domain (HSA, DRIP,
IHG/Hilton status) and focus on *this app's* specific workflow for each.

## Phase 1 basics

- **Income:** add each employer/source. Salary history is one row per rate with its effective date.
  Pay schedules are also effective-dated: to model the 2026 switch, keep the semi-monthly row and add
  a bi-weekly row effective the first day of the new cadence with one known pay date as the anchor.
  The pay-date calendar on the right shows every check and highlights three-check months. Set
  "Employment ended" when you leave; nothing is generated after it. "Odd checks" handles a single
  pay date that isn't a full period (a live-to-arrears switch paying one week): give it a percent of
  normal gross or the exact gross, and say whether fixed deductions shrink with it.
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
  balance and utilization. "Balance" records a statement or month-end balance. "Yearly cost vs
  value" ranks cards by annual fee plus interest (if the latest balance were carried at the APR)
  against the rewards value recorded this year, worst first, so a card that costs more than it
  returns stands out.
- **Dashboard:** bills due in the next 14 days, the next pay date, and per-account transfer needs.

## Phase 2: calculators

- **Income → benefit elections and W-4.** Open your W-2 employer and add one row per deduction
  from a stub (medical, dental, vision, 401(k) as a percent, and so on) with its tax treatment.
  Section 125 items reduce FICA and income tax; a traditional 401(k) reduces income tax only; Roth
  and after-tax items come out of net. Add a W-4 row with your filing status and any Step 2/3/4
  entries. Everything is effective-dated, so a change at open enrollment is a new row, not an edit.
- **Paycheck.** Pick the source and a pay date. *Estimate* shows the check and the year side by side;
  hover any line for its formula and expand "How this was calculated" for the steps. *What-if*
  changes salary, 401(k) percent, W-4 entries or a benefit amount and shows the check and year-end
  refund/owed next to today's. *Year* lists every check. *Bonus* estimates a supplemental payment
  flat (22%) or by the aggregate method. *Actual stubs* is where you type a real stub's lines and
  compare; the goal is net within $5, and any gap is listed line by line.
- **HSA.** Click months to cycle not eligible → self-only → family. The plan on the right shows the
  prorated limit with its formula, room after employer/payroll/direct contributions, and the
  monthly and per-paycheck pace to hit the limit (or your own target and date) by year end.
- **Loans.** Add a loan with rate, term and start date; leave the payment blank to compute it.
  Record a balance from each statement. *Projection* runs from the latest balance: payoff date,
  interest remaining, what extra principal saves, and what the original schedule says the balance
  should be now.
- **Tax tables.** Everything the paycheck and HSA math depends on, by year. Each January copy the
  previous year forward, update the figures from Pub 15-T, the SSA wage base, the Missouri
  withholding formula and the IRS limit notices, then tick Verified. Until then the Paycheck page
  shows an "unverified" warning.

## Phase 3: rewards

- **Cards → Add from catalog.** Pick a card; it arrives with its earn rules, spend thresholds, and
  its program's status paths. Then open *Rewards* on the card to correct anything against the
  issuer's current terms. Any card can have rules: a 2% cash-back card is one rule at 2× and 1¢.
- **Rewards → Programs.** Set each program's priority (1 = chase first), current and target tier,
  point value, and this year's nights / stays / program spend. Paths list every way to each tier.
- **Rewards → Card spend.** Type each card's monthly spend by category from statements. This is
  the year-to-date figure every threshold and card-spend path uses.
- **Rewards → Planned spend.** Monthly variable spend per category (restaurants, groceries…). With
  card-eligible bills this is the spend the plan allocates. Untick "can go on a card" for things
  that can't.
- **Rewards → Progress / Plan / Bills / Earnings.** Progress shows every path and threshold with
  the per-month figure. Plan allocates spend to goals in priority order and shows gaps with the
  other paths. Bills says card or bank per bill, weighing the bank-autopay discount. Earnings shows
  points and dollars per card net of fees. Use the year arrows to see next year's plan from zero.

## Phase 4: import and history

- **Import.** Pick the account or card the statement belongs to, leave the layout on "detect",
  choose the CSV/OFX/QFX file. The preview shows every line with a suggested category, bill, or
  transfer flag and why. Fix anything, untick "skip" lines you don't want, then Import. Lines
  already imported are marked as duplicates and left out. A line matched to a bill sets that
  month's actual on the Bills grid; card lines feed the Rewards card-spend figures. Undo removes a
  whole file's lines.
- **Transactions.** Browse by month or year, filter to uncategorized, search, edit a line's
  category/bill/transfer. Tick "always" when saving to create a rule that files every line with
  that merchant the same way, now and in future imports. The Rules panel lists and edits them.
- **Spending.** This month vs last, year to date, money in, uncategorized. Per-category table with
  drill-down to merchants and lines, and a month-over-month matrix.
- **Goals.** Financial goals read their current value from a metric (net worth, chosen account
  balances, HSA contributed, 401(k) deferrals, category totals, a loan balance) or a typed value.
  The on-track target is prorated by date; the bar is progress and the tick is elapsed time.
  Non-financial goals just carry a status.
- **Reports.** Year over year by category and by bill, net worth with 24 months of history, and
  CSV export (transactions, bills grid, accounts) through a save dialog.

## Phase 5: build-out

- **Dashboard** (nav item, formerly Home) now leads with what needs attention (alerts with links), then spending, net worth, next
  paycheck, rainy-day months covered, bills due, top categories, status goals, goals, transfer
  needs, and, when local AI is on, a button that writes the month's summary from the same numbers.
- **Investments.** Add a holding (ticker, brokerage account, DRIP on/off), enter buys and sells with
  a note on why. "Refresh prices & dividends" pulls closes and dividend events; dividends post from
  shares held on the ex-date and, with DRIP on, become reinvest trades. Details shows lots, realized
  gains with short/long term, wash-sale warnings with the earliest safe repurchase date, and the
  year's estimated tax on gains.
- **Owed to me.** One panel per person: month-by-month expected vs paid with Paid / Partial /
  Missed / Prepaid, one-off charges, payments applied to months (use Auto to fill the oldest first).
- **Paycheck → 1099.** Log each 1099 payment received and each estimated payment made; the panel
  shows the set-aside percent, the tax breakdown with steps, and the remaining quarterly amounts.
- **Accounts → Rainy-day fund.** Months of expenses covered by the accounts you marked.
- **Reports → Home & vehicles.** Record valuations by hand; they feed net worth.
- **Assistant.** Off until Ai:Enabled is set with your Ollama address and a tool-calling model. It
  only calls the listed functions and shows what it looked at under each answer.
