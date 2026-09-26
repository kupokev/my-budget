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
        var tBill = new Account { Name = "Chase T-Bill", Institution = "Chase", Type = AccountType.TreasuryBill, IsRainyDayFund = true };
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
            new Goal { Name = "Max the HSA", Kind = GoalKind.Financial, Metric = GoalMetric.HsaContributed, TargetAmount = 8_020.83m, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) },
            new Goal { Name = "Rainy-day fund to $30K", Kind = GoalKind.Financial, Metric = GoalMetric.AccountBalances, StartValue = 18_000m, TargetAmount = 30_000m, StartDate = new(2026, 1, 1), EndDate = new(2027, 6, 30), AccountIds = $"{tBill.Id},{wealthfront.Id}" },
            new Goal { Name = "Restaurants under $6K", Kind = GoalKind.Financial, Metric = GoalMetric.CategoryOutflow, CategoryId = restaurants.Id, TargetAmount = 6_000m, LowerIsBetter = true, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) },
            new Goal { Name = "Read 12 books", Kind = GoalKind.NonFinancial, Status = GoalStatus.InProgress, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31) });

        db.AccountBalances.AddRange(
            new AccountBalance { Account = pnc, AsOf = new(2026, 9, 1), Balance = 2_450m },
            new AccountBalance { Account = automatedBills, AsOf = new(2026, 9, 1), Balance = 610m },
            new AccountBalance { Account = chaseMain, AsOf = new(2026, 9, 1), Balance = 3_200m });

        await db.SaveChangesAsync(ct);
    }
}
