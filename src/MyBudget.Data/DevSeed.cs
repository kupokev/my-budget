using Microsoft.EntityFrameworkCore;
using MyBudget.Domain;

namespace MyBudget.Data;

/// <summary>
/// Development-only starter data shaped like the 2026 sheet so screens have something to show.
/// Amounts are placeholders, not real numbers. Never runs against PostgreSQL.
/// </summary>
public static class DevSeed
{
    public static async Task SeedAsync(BudgetDbContext db, CancellationToken ct = default)
    {
        if (await db.Accounts.AnyAsync(ct)) return;

        var pnc = new Account { Name = "PNC Checking", Institution = "PNC", Type = AccountType.Checking, MinimumBalance = 2000m };
        var premierSavings = new Account { Name = "Chase Premier Savings", Institution = "Chase", Type = AccountType.Savings, MinimumBalance = 500m };
        var chaseMain = new Account { Name = "Chase Main", Institution = "Chase", Type = AccountType.Checking };
        var automatedBills = new Account { Name = "Chase Automated Bills", Institution = "Chase", Type = AccountType.Checking, TransferCadence = TransferCadence.PerPaycheck };
        var tBill = new Account { Name = "Chase T-Bill", Institution = "Chase", Type = AccountType.Brokerage, IsRainyDayFund = true, Notes = "Treasury bills bought through the brokerage" };
        var wealthfront = new Account { Name = "Wealthfront", Institution = "Wealthfront", Type = AccountType.HighYieldSavings, IsRainyDayFund = true };
        var fidelityHsa = new Account { Name = "Fidelity HSA", Institution = "Fidelity", Type = AccountType.Hsa };
        var inspiraHsa = new Account { Name = "Inspira HSA (employer)", Institution = "Inspira", Type = AccountType.Hsa };
        db.Accounts.AddRange(pnc, premierSavings, chaseMain, automatedBills, tBill, wealthfront, fidelityHsa, inspiraHsa);

        var employer = new IncomeSource
        {
            Name = "Ridgeline Partners", Type = IncomeSourceType.W2Salary,
            SalaryRates =
            [
                new SalaryRate { AnnualAmount = 165_128m, EffectiveDate = new(2025, 1, 1) },
                new SalaryRate { AnnualAmount = 170_000m, EffectiveDate = new(2026, 2, 1) },
            ],
            PaySchedules =
            [
                new PaySchedule { Frequency = PayFrequency.SemiMonthly, EffectiveDate = new(2025, 1, 1), AnchorPayDate = new(2025, 1, 15), FirstPayDay = 15, SecondPayDay = 31 },
                new PaySchedule { Frequency = PayFrequency.BiWeekly, EffectiveDate = new(2026, 2, 1), AnchorPayDate = new(2026, 2, 6) },
            ],
            // Placeholder elections: replace with the real per-check amounts from a stub.
            Deductions =
            [
                new DeductionElection { Name = "Medical", Kind = DeductionKind.Medical, Treatment = DeductionTreatment.PreTaxSection125, AmountPerCheck = 160m, EffectiveDate = new(2025, 1, 1) },
                new DeductionElection { Name = "Dental", Kind = DeductionKind.Dental, Treatment = DeductionTreatment.PreTaxSection125, AmountPerCheck = 18m, EffectiveDate = new(2025, 1, 1) },
                new DeductionElection { Name = "Vision", Kind = DeductionKind.Vision, Treatment = DeductionTreatment.PreTaxSection125, AmountPerCheck = 6m, EffectiveDate = new(2025, 1, 1) },
                new DeductionElection { Name = "Life / disability", Kind = DeductionKind.Life, Treatment = DeductionTreatment.PostTax, AmountPerCheck = 12m, EffectiveDate = new(2025, 1, 1) },
                new DeductionElection { Name = "401(k) 6%", Kind = DeductionKind.Retirement401k, Treatment = DeductionTreatment.PreTaxRetirement, PercentOfGross = 0.06m, EffectiveDate = new(2025, 1, 1) },
            ],
            Withholdings =
            [
                new WithholdingElection { EffectiveDate = new(2025, 1, 1), FederalStatus = FederalFilingStatus.SingleOrMarriedFilingSeparately, MissouriStatus = MissouriFilingStatus.Single },
            ],
        };
        db.IncomeSources.AddRange(employer,
            new IncomeSource { Name = "Chroma", Type = IncomeSourceType.Contract1099 },
            new IncomeSource { Name = "Alphanomix", Type = IncomeSourceType.Contract1099 },
            new IncomeSource { Name = "Robin (reimbursement)", Type = IncomeSourceType.Reimbursement });

        var utilities = new Category { Name = "Utilities" };
        var housing = new Category { Name = "Housing", IsCardEligible = false };
        var subscriptions = new Category { Name = "Subscriptions" };
        var insurance = new Category { Name = "Insurance" };
        var memberships = new Category { Name = "Memberships" };
        var restaurants = new Category { Name = "Restaurants", PlannedMonthly = 600m };
        var groceries = new Category { Name = "Groceries", PlannedMonthly = 700m };
        var gas = new Category { Name = "Gas", PlannedMonthly = 250m };
        var travel = new Category { Name = "Travel", PlannedMonthly = 400m };
        var other = new Category { Name = "Other spending", PlannedMonthly = 800m };
        db.Categories.AddRange(utilities, housing, subscriptions, insurance, memberships, restaurants, groceries, gas, travel, other,
            new Category { Name = "Transportation" }, new Category { Name = "Hotels (IHG)" }, new Category { Name = "Hotels (Hilton)" },
            new Category { Name = "Online retail" }, new Category { Name = "Streaming" }, new Category { Name = "Drugstore" }, new Category { Name = "Entertainment" });
        await db.SaveChangesAsync(ct);

        // Programs and catalog cards, exactly as "Add from catalog" would create them.
        foreach (var program in CardCatalog.Programs()) db.LoyaltyPrograms.Add(program);
        await db.SaveChangesAsync(ct);
        var chaseIhg = await CatalogService.AddCardAsync(db, "chase-ihg-premier", chaseMain, ct);
        chaseIhg.StatementDay = 12; chaseIhg.DueDay = 9; chaseIhg.AnnualFeeMonth = 4;
        var hiltonSurpass = await CatalogService.AddCardAsync(db, "amex-hilton-surpass", chaseMain, ct);
        hiltonSurpass.StatementDay = 20; hiltonSurpass.DueDay = 15; hiltonSurpass.AnnualFeeMonth = 6;
        var sapphire = await CatalogService.AddCardAsync(db, "chase-sapphire-preferred", chaseMain, ct);
        var freedom = await CatalogService.AddCardAsync(db, "chase-freedom-unlimited", chaseMain, ct);
        var citi = await CatalogService.AddCardAsync(db, "citi-double-cash", chaseMain, ct);
        var quicksilver = await CatalogService.AddCardAsync(db, "capital-one-quicksilver", chaseMain, ct);
        var savor = await CatalogService.AddCardAsync(db, "capital-one-savorone", chaseMain, ct);
        var wf = await CatalogService.AddCardAsync(db, "wells-fargo-active-cash", chaseMain, ct);
        var discover = await CatalogService.AddCardAsync(db, "discover-it", chaseMain, ct);
        var amexBce = await CatalogService.AddCardAsync(db, "amex-blue-cash-everyday", chaseMain, ct);
        db.Cards.Add(new Card { Name = "HICV", Issuer = "Comenity", Network = "Visa", StatementDay = 27, DueDay = 24, PayingAccount = chaseMain, Apr = 24.99m, CreditLimit = 10_000m });

        // Placeholder year-to-date card spend (RWD-3) and program activity; replace from statements.
        foreach (var m in Enumerable.Range(1, 9))
        {
            var period = new DateOnly(2026, m, 1);
            db.CardSpend.AddRange(
                new CardSpend { Card = chaseIhg, Period = period, Category = groceries, Amount = 650m },
                new CardSpend { Card = chaseIhg, Period = period, Category = gas, Amount = 240m },
                new CardSpend { Card = chaseIhg, Period = period, Category = other, Amount = 500m },
                new CardSpend { Card = hiltonSurpass, Period = period, Category = restaurants, Amount = 550m },
                new CardSpend { Card = hiltonSurpass, Period = period, Category = travel, Amount = 300m });
        }
        var ihgProgram = await db.LoyaltyPrograms.FirstAsync(p => p.Name == CardCatalog.Ihg, ct);
        var hiltonProgram = await db.LoyaltyPrograms.FirstAsync(p => p.Name == CardCatalog.Hilton, ct);
        ihgProgram.CurrentTier = "Platinum"; ihgProgram.PointsBalance = 85_000m;
        ihgProgram.Progress.Add(new LoyaltyProgress { Year = 2026, Nights = 12, QualifyingPoints = 30_000m });
        hiltonProgram.CurrentTier = "Gold"; hiltonProgram.PointsBalance = 120_000m;
        hiltonProgram.Progress.Add(new LoyaltyProgress { Year = 2026, Nights = 18, Stays = 9, ProgramSpend = 4_200m });

        db.Bills.AddRange(
            new Bill { Name = "Mortgage", Category = housing, DueDay = 1, ProjectedAmount = 2_100m, IsAutopay = true, PaymentMethod = PaymentMethodKind.Account, PaymentAccount = chaseMain, FundingAccount = chaseMain, IsCardEligible = false },
            new Bill { Name = "Water", Category = utilities, DueDay = 20, ProjectedAmount = 60m, IsAutopay = true, PaymentAccount = automatedBills, FundingAccount = automatedBills },
            new Bill { Name = "Sewer", Category = utilities, DueDay = 20, ProjectedAmount = 45m, IsAutopay = true, PaymentAccount = automatedBills, FundingAccount = automatedBills },
            new Bill { Name = "Electric", Category = utilities, DueDay = 18, ProjectedAmount = 140m, IsAutopay = true, PaymentAccount = automatedBills, FundingAccount = automatedBills },
            new Bill { Name = "AT&T", Category = utilities, DueDay = 6, ProjectedAmount = 85m, IsAutopay = true, PaymentAccount = automatedBills, FundingAccount = automatedBills, BankAutopayDiscount = 5m, Notes = "$5/mo discount for bank autopay vs. card" },
            new Bill { Name = "Hulu", Category = subscriptions, DueDay = 11, ProjectedAmount = 18.99m, IsAutopay = true, PaymentMethod = PaymentMethodKind.Card, PaymentCard = chaseIhg, FundingAccount = automatedBills },
            new Bill { Name = "Car insurance", Category = insurance, Frequency = BillFrequency.SemiAnnual, DueDay = 15, AnchorDueDate = new(2026, 3, 15), ProjectedAmount = 612m, PaymentMethod = PaymentMethodKind.Card, PaymentCard = hiltonSurpass, FundingAccount = premierSavings },
            new Bill { Name = "AAA", Category = memberships, Frequency = BillFrequency.Annual, DueDay = 1, AnchorDueDate = new(2026, 5, 1), ProjectedAmount = 120m, PaymentMethod = PaymentMethodKind.Card, PaymentCard = chaseIhg, FundingAccount = premierSavings },
            new Bill { Name = "Costco", Category = memberships, Frequency = BillFrequency.Annual, DueDay = 1, AnchorDueDate = new(2026, 11, 1), ProjectedAmount = 65m, PaymentMethod = PaymentMethodKind.Card, PaymentCard = chaseIhg, FundingAccount = premierSavings });

        db.HsaYears.Add(new HsaYear
        {
            Year = 2026,
            Months = Enumerable.Range(1, 12).Select(m => new HsaMonth { Month = m, Tier = m == 2 ? HsaTier.NotEligible : HsaTier.Family }).ToList(),
            Contributions =
            [
                new HsaContribution { Date = new(2026, 1, 15), Amount = 1_288m, Source = HsaContributionSource.Employer, Account = inspiraHsa, Notes = "Inspira employer contribution" },
                new HsaContribution { Date = new(2026, 8, 31), Amount = 2_805m, Source = HsaContributionSource.Direct, Account = fidelityHsa, Notes = "Fidelity direct through August" },
            ],
        });

        db.Loans.Add(new Loan
        {
            Name = "Mortgage", Kind = LoanKind.Mortgage, Lender = "Placeholder Bank", OriginalPrincipal = 300_000m, AnnualRate = 0.065m, TermMonths = 360,
            StartDate = new(2022, 6, 1), ScheduledPayment = 1_896.20m,
            Balances = [new LoanBalance { AsOf = new(2026, 9, 1), Balance = 283_000m }],
        });

        db.CategoryRules.AddRange(
            new CategoryRule { Pattern = "HULU", CategoryId = subscriptions.Id, Priority = 10 },
            new CategoryRule { Pattern = "AMEREN", Priority = 10 },
            new CategoryRule { Pattern = "PAYMENT THANK YOU", MarkAsTransfer = true, Priority = 1 });
        db.Goals.AddRange(
            new Goal { Name = "Net worth +$25K this year", Kind = GoalKind.Financial, Metric = GoalMetric.NetWorth, StartValue = 120_000m, TargetAmount = 145_000m, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) },
            new Goal { Name = "Max the HSA", Kind = GoalKind.Financial, Metric = GoalMetric.AccountTypeContributions, AccountType = AccountType.Hsa, TargetAmount = 8_020.83m, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) },
            new Goal { Name = "Rainy-day fund to $30K", Kind = GoalKind.Financial, Metric = GoalMetric.AccountBalances, StartValue = 18_000m, TargetAmount = 30_000m, StartDate = new(2026, 1, 1), EndDate = new(2027, 6, 30), AccountIds = $"{tBill.Id},{wealthfront.Id}" },
            new Goal { Name = "Restaurants under $6K", Kind = GoalKind.Financial, Metric = GoalMetric.CategoryOutflow, CategoryId = restaurants.Id, TargetAmount = 6_000m, LowerIsBetter = true, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) },
            new Goal { Name = "Read 12 books", Kind = GoalKind.NonFinancial, Status = GoalStatus.InProgress, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) });

        var brokerage = new Account { Name = "Fidelity Brokerage", Institution = "Fidelity", Type = AccountType.Brokerage };
        db.Accounts.Add(brokerage);
        db.Holdings.Add(new Holding
        {
            Ticker = "VTI", Name = "Vanguard Total Stock Market ETF", Account = brokerage, Drip = true,
            Trades = [new Trade { Date = new(2025, 3, 3), Kind = TradeKind.Buy, Shares = 10, Price = 280.00m, Fees = 0, Notes = "placeholder lot" }],
        });
        db.Assets.Add(new Asset { Name = "House", Kind = AssetKind.Home, Values = [new AssetValue { AsOf = new(2026, 9, 1), Value = 385_000m }] });
        db.Assets.Add(new Asset { Name = "Car", Kind = AssetKind.Vehicle, Values = [new AssetValue { AsOf = new(2026, 9, 1), Value = 18_500m }] });
        var amexLoanBill = new Bill { Name = "Amex loan", Category = housing, DueDay = 5, ProjectedAmount = 250m, IsAutopay = true, PaymentAccount = chaseMain, FundingAccount = chaseMain, IsCardEligible = false, Notes = "Sam repays 100% each month" };
        db.Bills.Add(amexLoanBill);
        db.People.AddRange(
            new Person
            {
                Name = "Sam",
                Obligations = [new Obligation { Description = "Amex loan payment", Bill = amexLoanBill, ShareOfBill = 1.0m, StartPeriod = new(2026, 1, 1) }],
                Payments = Enumerable.Range(1, 8).Select(m => new ReceivablePayment { Date = new(2026, m, 6), Amount = 250m, Allocations = [new PaymentAllocation { Period = new(2026, m, 1), Amount = 250m }] }).ToList(),
            },
            new Person
            {
                Name = "Robin",
                Obligations = [new Obligation { Description = "Phone line", MonthlyAmount = 45m, StartPeriod = new(2026, 1, 1) }],
                Charges = [new ReceivableCharge { Date = new(2026, 7, 12), Amount = 120m, Description = "Concert tickets" }],
                Payments =
                [
                    new ReceivablePayment { Date = new(2026, 1, 10), Amount = 135m, Notes = "prepaid Jan–Mar", Allocations = [new() { Period = new(2026, 1, 1), Amount = 45m }, new() { Period = new(2026, 2, 1), Amount = 45m }, new() { Period = new(2026, 3, 1), Amount = 45m }] },
                    new ReceivablePayment { Date = new(2026, 4, 8), Amount = 45m, Allocations = [new() { Period = new(2026, 4, 1), Amount = 45m }] },
                    new ReceivablePayment { Date = new(2026, 8, 1), Amount = 120m, Allocations = [new() { Period = null, Amount = 120m }] },
                ],
            });
        db.IncomeReceipts.AddRange(
            new IncomeReceipt { IncomeSource = db.IncomeSources.Local.First(s => s.Name == "Chroma"), Date = new(2026, 3, 15), Amount = 4_000m },
            new IncomeReceipt { IncomeSource = db.IncomeSources.Local.First(s => s.Name == "Alphanomix"), Date = new(2026, 6, 30), Amount = 6_500m });

        // Starting balances for every account on Jan 1 (so each has a history), then September statement snapshots for a few.
        db.AccountBalances.AddRange(
            new AccountBalance { Account = pnc, AsOf = new(2026, 1, 1), Balance = 2_300m },
            new AccountBalance { Account = premierSavings, AsOf = new(2026, 1, 1), Balance = 1_150m },
            new AccountBalance { Account = chaseMain, AsOf = new(2026, 1, 1), Balance = 2_900m },
            new AccountBalance { Account = automatedBills, AsOf = new(2026, 1, 1), Balance = 540m },
            new AccountBalance { Account = tBill, AsOf = new(2026, 1, 1), Balance = 12_000m },
            new AccountBalance { Account = wealthfront, AsOf = new(2026, 1, 1), Balance = 6_000m },
            new AccountBalance { Account = fidelityHsa, AsOf = new(2026, 1, 1), Balance = 4_800m },
            new AccountBalance { Account = inspiraHsa, AsOf = new(2026, 1, 1), Balance = 1_100m },
            new AccountBalance { Account = brokerage, AsOf = new(2026, 1, 1), Balance = 0m },
            new AccountBalance { Account = pnc, AsOf = new(2026, 8, 31), Balance = 2_450m },
            new AccountBalance { Account = automatedBills, AsOf = new(2026, 8, 31), Balance = 610m },
            new AccountBalance { Account = chaseMain, AsOf = new(2026, 8, 31), Balance = 3_200m });

        await db.SaveChangesAsync(ct);
        await SeedTransactionsAsync(db, chaseMain, automatedBills, ct);
    }

    /// <summary>August and September bank activity: paychecks, bills, a few purchases, and the monthly transfer to the bills account entered by hand and reconciled with the bank's line.</summary>
    private static async Task SeedTransactionsAsync(BudgetDbContext db, Account chaseMain, Account automatedBills, CancellationToken ct)
    {
        var cats = await db.Categories.ToDictionaryAsync(c => c.Name, ct);
        var bills = await db.Bills.ToDictionaryAsync(b => b.Name, ct);
        var batch = new ImportBatch { FileName = "seed-chase.csv", Format = ImportFormat.Csv, Profile = "chase-checking", ImportedAt = DateTime.Now, Account = chaseMain, RowCount = 0 };
        db.ImportBatches.Add(batch);

        Transaction Imported(Account acct, DateOnly date, decimal amount, string desc, string merchant, string? category = null, string? bill = null, bool transfer = false) => new()
        {
            Account = acct, Date = date, Amount = amount, Description = desc, Merchant = merchant, Origin = TransactionOrigin.Imported, ImportBatch = batch,
            CategoryId = category is not null ? cats[category].Id : bill is not null ? bills[bill].CategoryId : null, BillId = bill is not null ? bills[bill].Id : null,
            IsTransfer = transfer, ExternalId = $"seed:{acct.Name}:{date:yyyyMMdd}:{amount}:{desc}",
        };

        var lines = new List<Transaction>();
        foreach (var (y, m) in new[] { (2026, 8), (2026, 9) })
        {
            var actual = m == 8 ? (Electric: 149.50m, Water: 61.00m) : (Electric: 151.20m, Water: 58.40m);
            lines.AddRange(
            [
                Imported(chaseMain, new(y, m, 1), -2_100m, "MORTGAGE PMT PLACEHOLDER BANK", "Placeholder Bank", bill: "Mortgage"),
                Imported(chaseMain, new(y, m, 4), 4_222.07m, "RIDGELINE PARTNERS PAYROLL", "Ridgeline Partners Payroll"),
                Imported(chaseMain, new(y, m, 18), 4_222.07m, "RIDGELINE PARTNERS PAYROLL", "Ridgeline Partners Payroll"),
                Imported(chaseMain, new(y, m, 5), -250m, "AMEX LOAN PAYMENT", "Amex Loan", bill: "Amex loan"),
                Imported(chaseMain, new(y, m, 9), -84.12m, "KROGER #0456", "Kroger", "Groceries"),
                Imported(chaseMain, new(y, m, 13), -32.10m, "TST* PAPPYS SMOKEHOUSE ST LOUIS MO", "Pappys Smokehouse", "Restaurants"),
                Imported(chaseMain, new(y, m, 21), -46.75m, "SHELL OIL 57444 ST LOUIS MO", "Shell Oil", "Gas"),
                Imported(chaseMain, new(y, m, 2), -330m, "Online Transfer to CHK ...4412", "Online Transfer", transfer: true),
                Imported(automatedBills, new(y, m, 2), 330m, "Online Transfer from CHK ...9901", "Online Transfer", transfer: true),
                Imported(automatedBills, new(y, m, 6), -85m, "ATT PAYMENT", "Att Payment", bill: "AT&T"),
                Imported(automatedBills, new(y, m, 18), -actual.Electric, "AMEREN MISSOURI", "Ameren Missouri", bill: "Electric"),
                Imported(automatedBills, new(y, m, 20), -actual.Water, "CITY WATER UTILITY", "City Water Utility", bill: "Water"),
                Imported(automatedBills, new(y, m, 20), -45m, "MSD SEWER", "Msd Sewer", bill: "Sewer"),
            ]);
            // The same transfer, entered by hand on the 1st (two-sided) and reconciled with the bank's lines on the 2nd.
            var outRow = new Transaction { Account = chaseMain, Date = new(y, m, 1), Amount = -330m, Description = "Transfer out to Chase Automated Bills", Merchant = "Transfer out to Chase Automated Bills", Origin = TransactionOrigin.Manual, IsTransfer = true, IsManuallyCategorized = true, ExternalId = $"manual:seed:{y}{m}:out", CounterpartyAccount = automatedBills, Notes = "monthly bills" };
            var inRow = new Transaction { Account = automatedBills, Date = new(y, m, 1), Amount = 330m, Description = "Transfer in from Chase Main", Merchant = "Transfer in from Chase Main", Origin = TransactionOrigin.Manual, IsTransfer = true, IsManuallyCategorized = true, ExternalId = $"manual:seed:{y}{m}:in", CounterpartyAccount = chaseMain, Notes = "monthly bills" };
            lines.AddRange([outRow, inRow]);
            // Bill actuals those lines represent.
            foreach (var (name, amt) in new[] { ("Mortgage", 2_100m), ("Amex loan", 250m), ("AT&T", 85m), ("Electric", actual.Electric), ("Water", actual.Water), ("Sewer", 45m) })
                db.BillPeriods.Add(new BillPeriod { BillId = bills[name].Id, Period = new(y, m, 1), ActualAmount = amt, Notes = "from import" });
        }
        db.Transactions.AddRange(lines);
        batch.RowCount = lines.Count(l => l.Origin == TransactionOrigin.Imported); batch.ImportedCount = batch.RowCount;
        await db.SaveChangesAsync(ct);

        // Link the manual pairs to each other and reconcile each with the bank's line.
        foreach (var (y, m) in new[] { (2026, 8), (2026, 9) })
        {
            var outRow = lines.Single(l => l.Origin == TransactionOrigin.Manual && l.Date == new DateOnly(y, m, 1) && l.Amount < 0);
            var inRow = lines.Single(l => l.Origin == TransactionOrigin.Manual && l.Date == new DateOnly(y, m, 1) && l.Amount > 0);
            outRow.LinkedTransactionId = inRow.Id; inRow.LinkedTransactionId = outRow.Id;
            var bankOut = lines.Single(l => l.Origin == TransactionOrigin.Imported && l.Account == chaseMain && l.Date == new DateOnly(y, m, 2) && l.Amount == -330m);
            var bankIn = lines.Single(l => l.Origin == TransactionOrigin.Imported && l.Account == automatedBills && l.Date == new DateOnly(y, m, 2) && l.Amount == 330m);
            outRow.ReconciledWithId = bankOut.Id; bankOut.ReconciledWithId = outRow.Id; bankOut.CounterpartyAccount = automatedBills;
            inRow.ReconciledWithId = bankIn.Id; bankIn.ReconciledWithId = inRow.Id; bankIn.CounterpartyAccount = chaseMain;
        }
        await db.SaveChangesAsync(ct);
    }
}
