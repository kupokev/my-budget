# User guide

How to actually use the app day to day — importing a statement, logging a trade, marking an
account as rainy-day fund, reading the rewards progress view, etc. Written for the single user of
this app (not a general audience), so it can assume familiarity with the domain (HSA, DRIP,
IHG/Hilton status) and focus on *this app's* specific workflow for each.

## Starting empty instead of with sample data

In Development the API seeds sample accounts, bills, cards and so on so every page has something to
show. To start from nothing, run the API with the seed switched off:

```
Database__SkipDevSeed=true dotnet run --project src/MyBudget.Api --launch-profile http
```

(Reference tables for tax rules and limits are still loaded; only the sample data is skipped. The
in-memory database resets on every restart either way.)

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
  The double-arrow icon opens one Record form: a starting/current balance, a transfer in, or a
  transfer out. For transfers, pick the other account (the matching entry is written there too and
  the pair deletes together) or "Other" for money from or to outside. Long/Short on the Dashboard
  compares transfers this month to the monthly need.
- **Budget:** everything you plan to spend money on. Dated lines (mortgage, insurance, subscriptions)
  behave as bills always did, with a due day and a year grid. Set a line's frequency to **Variable**
  for planned spend with no due date and no biller: groceries, restaurants, gas. A variable line just
  carries a monthly figure. Give a line a label to budget one merchant apart from its category, so
  Amazon can be planned and routed separately from the rest of General merchandise.
  The year grid is the old bill tab. Click a month cell to open that month's editor:
  type the actual and press Enter for the quick case, or also set this month's due date or expected
  amount when they differ from the bill's defaults (a blank field means "use the default"). A blue
  dot on a cell marks a month with an override; hover a cell for its details. Grey parentheses are
  expected amounts for months the bill is due; red actuals are over. A bill that stops (a payment
  plan ending in October) gets an End date on the bill, which zeroes later months automatically.
  Projected, Category, Due, Paid via, Funded from and Avg actual are hidden by default: use the
  Columns menu, or hover the bill name (projected shows in each due month's grey parentheses). The
  Category dropdown filters the grid; categories themselves are managed under Admin → Categories. "Paid via" is the account or card that pays; "Funded from" is where the money really
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

- **Cards → New card**, then the ★ icon for its rewards: the loyalty program it earns into (blank
  for cash back, with a point value of 1¢ so points come out in dollars), an earn rule per category
  and optionally per label, plus one with neither as the base rate, and any calendar-year spend
  thresholds. A rule with a label beats one with only a category, so "General merchandise at Amazon
  5×" and "General merchandise 1×" live side by side. Earn rules and
  thresholds take an optional year range, so a promo rate for one year doesn't skew other years.
- **Admin → Categories & Labels.** As well as naming the buckets, this is where planned variable
  spend lives: what you expect to put on cards each month beyond tracked bills (restaurants,
  groceries, gas). It is not derived from bills. That figure plus card-eligible bill accruals is the
  whole pool the rewards plan allocates, and the table totals it for you. Untick "can go on a card"
  for anything that can't take one.
- **Admin → Loyalty programs.** Set up once and rarely touched, so it lives in Admin. One program per points currency (IHG One Rewards, Hilton Honors, Delta
  SkyMiles). Set each program's priority (1 = chase first), current and target tier,
  point value, and this year's nights / stays / program spend. Paths list every way to each tier.
- **Rewards status → Progress / Plan / Budget lines / Earnings.** Progress shows each program you're in,
  the tier you hold and what that tier gets you, then every path and threshold with
  the per-month figure. Plan allocates spend to goals in priority order and shows gaps with the
  other paths. Budget lines says card or bank per line, weighing the bank-autopay discount. Earnings shows
  points and dollars per card net of fees. Use the year arrows to see next year's plan from zero.
- **Calculators → Paycheck what-if.** Was a tab on Paycheck. Change a salary, a 401(k) percent, a
  W-4 line or a benefit amount and see the check and the year side by side against what you have
  configured today. Nothing here is saved; the real elections live under Admin → Income sources.

- **Calculators → Points vs cash.** Standing at a booking page, price the stay both ways and enter
  the two totals. It returns cents per point and tells you to pay cash under 0.45¢, calls it a wash
  between 0.45¢ and 0.55¢, and says use the points at 0.55¢ and up. Enter the cash price all-in
  including taxes, because an award stay doesn't pay them. Open the earning section and it also
  subtracts the points a cash stay would have earned, which is usually worth most of a band.
  Enter totals for the whole stay so a fourth- or fifth-night-free benefit is already counted.

## Phase 4: import and history

- **Import.** Pick the account or card the statement belongs to, leave the layout on "detect",
  choose the CSV/OFX/QFX file. The preview shows every line with a suggested category, bill, or
  transfer flag and why. Fix anything, untick "skip" lines you don't want, then Import. Lines
  already imported are marked as duplicates and left out. A line matched to a bill sets that
  month's actual on the Budget grid; card lines feed the Rewards card-spend figures. Undo removes a
  whole file's lines.
- **Labels** (Admin → Categories) say *where* a purchase happened next to the category's *what kind*:
  Amazon vs Costco within General merchandise, or IHG vs Hilton within Travel. Give a label a usual
  category and a planned monthly amount and the rewards plan splits that category, sending the
  labelled part to whichever card pays best for it. A categorization rule can apply a label
  automatically, so every Amazon line is tagged on import.
- **Transactions.** The one place transactions are entered. **New transaction** takes an account or
  a card, a date, a signed amount (negative is money out) and a description. Tick *Transfer* and pick
  another account to write both sides at once. Card charges you enter here count towards your rewards
  thresholds straight away, and the Cards page shows what each card has taken this month and last.
- **Transactions (filtering and editing).** Browse by month or year, filter to uncategorized or to entries that still need
  reconciling, search, edit a line's category/bill/transfer. A transfer you typed on Accounts shows
  here as "manual" and "unreconciled" until a statement import brings the bank's line for it; the
  import links them automatically when amount and date (±3 days) match, or use "reconcile…" to pick
  the line yourself. A reconciled pair counts once in balances. A deposit that is someone paying you back: edit it and pick the person under "Repayment from"; the payment lands on their Owed-to-me ledger (oldest months first) and the line stops counting as income. Tick "always" when saving to create a rule that files every line with
  that merchant the same way, now and in future imports. The Rules panel lists and edits them.
- **Goals.** Financial goals read their current value from a metric (net worth, chosen account
  balances, contributions into an account type such as HSA or Roth IRA, 401(k) deferrals, category
  totals, a loan balance) or a typed value.
  The on-track target is prorated by date; the bar is progress and the tick is elapsed time.
  Non-financial goals just carry a status.
- **Assets.** Under Wealth. Houses, vehicles and anything else valued by hand. Use ＋ on a row to
  record a value for a date; recording the same date twice replaces that entry. The ▸ button opens
  the valuation history: a bar per record and a table showing each change and percent change. Attach
  the loans secured against an asset there too, and the page shows equity as the value less what is
  still owed. The link lives on the loan, so it can also be set under Admin → Loans with each loan's
  *Secured against* picker; a loan can only be secured against one asset, but an asset can carry several. Net worth counts the asset and the loans separately, so attaching one changes nothing
  but the display.

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
- **Owed to me.** Obligations can repeat every month, quarter, four or six months, or yearly, counting
  from their first-due month; months in between show nothing expected. One panel per person: month-by-month expected vs paid with Paid / Partial /
  Missed / Prepaid, one-off charges, payments applied to months (use Auto to fill the oldest first).
- **Paycheck → 1099.** Log each 1099 payment received and each estimated payment made; the panel
  shows the set-aside percent, the tax breakdown with steps, and the remaining quarterly amounts.
- **Accounts → Rainy-day fund.** Months of expenses covered by the accounts you marked.
- **Reports → Home & vehicles.** Record valuations by hand; they feed net worth.
- **Assistant.** Off until Ai:Enabled is set with your Ollama address and a tool-calling model. It
  only calls the listed functions and shows what it looked at under each answer.
