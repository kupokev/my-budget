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
        var restaurants = new Category { Name = "Restaurants" };
        var groceries = new Category { Name = "Groceries" };
        var gas = new Category { Name = "Gas" };
        var travel = new Category { Name = "Travel" };
        var other = new Category { Name = "Other spending" };
        var onlineRetail = new Category { Name = "Online retail" };
        var streaming = new Category { Name = "Streaming" };
        var drugstore = new Category { Name = "Drugstore" };
        var merchandise = new Category { Name = "General merchandise" };
        db.Categories.AddRange(utilities, housing, subscriptions, insurance, memberships, restaurants, groceries, gas, travel, other,
            onlineRetail, streaming, drugstore, merchandise,
            new Category { Name = "Transportation" }, new Category { Name = "Entertainment" });
        // Labels: where a purchase happened. "General merchandise" at Amazon earns differently from the same
        // category at Costco, so a budget line can name the label and carry its own earn rule.
        var amazon = new Label { Name = "Amazon", Category = merchandise };
        var costco = new Label { Name = "Costco", Category = merchandise };
        var ihgLabel = new Label { Name = "IHG", Category = travel };
        var hiltonLabel = new Label { Name = "Hilton", Category = travel };
        db.Labels.AddRange(amazon, costco, ihgLabel, hiltonLabel);
        await db.SaveChangesAsync(ct);

        // Loyalty programs, then cards linked to them. This is exactly what "New card" plus the rewards
        // editor (Cards → ★) and Rewards → Programs let you build by hand; nothing here is special.
        var ihgProgram = new LoyaltyProgram
        {
            // Diamond was earned by spending $40,000 on the card in 2025, so it is held all through 2026
            // without spending again; the 2027 plan is where the $40,000 shows up as a goal once more.
            Name = "IHG One Rewards", PointValueCents = 0.5m, Priority = 1, TargetTier = "Diamond", CurrentTier = "Diamond", PointsBalance = 85_000m,
            Tiers = [
                new() { Name = "Club", Rank = 0 },
                new() { Name = "Silver", Rank = 1, Benefits = "10% bonus points, late checkout when available" },
                new() { Name = "Gold", Rank = 2, Benefits = "20% bonus points, room upgrade when available" },
                new() { Name = "Platinum", Rank = 3, Benefits = "60% bonus points, room upgrade, guaranteed late checkout, free breakfast at some brands" },
                new() { Name = "Diamond", Rank = 4, Benefits = "100% bonus points, best available upgrade incl. suites, free breakfast, welcome amenity, guaranteed 4pm checkout" }],
            Progress = [new LoyaltyProgress { Year = 2026, Nights = 12, QualifyingPoints = 30_000m }, new LoyaltyProgress { Year = 2025, Nights = 21, ProgramSpend = 40_000m }],
        };
        var hiltonProgram = new LoyaltyProgram
        {
            Name = "Hilton Honors", PointValueCents = 0.5m, Priority = 2, TargetTier = "Diamond", CurrentTier = "Gold", PointsBalance = 120_000m,
            Tiers = [
                new() { Name = "Member", Rank = 0 },
                new() { Name = "Silver", Rank = 1, Benefits = "20% bonus points, 5th night free on awards, free water" },
                new() { Name = "Gold", Rank = 2, Benefits = "80% bonus points, free breakfast or daily credit, space-available upgrades, 5th night free" },
                new() { Name = "Diamond", Rank = 3, Benefits = "100% bonus points, executive lounge access, upgrades incl. suites, guaranteed room availability with 48h notice" }],
            Progress = [new LoyaltyProgress { Year = 2026, Nights = 18, Stays = 9, ProgramSpend = 4_200m }],
        };
        var deltaProgram = new LoyaltyProgram
        {
            Name = "Delta SkyMiles", PointValueCents = 1.2m, Priority = 3, CurrentTier = "Member", PointsBalance = 34_000m,
            Tiers = [new() { Name = "Member", Rank = 0 }, new() { Name = "Silver", Rank = 1 }, new() { Name = "Gold", Rank = 2 }, new() { Name = "Platinum", Rank = 3 }],
        };
        var hertzProgram = new LoyaltyProgram
        {
            Name = "Hertz Gold Plus Rewards", PointValueCents = 0.4m, Priority = 4, TargetTier = "Five Star",
            Tiers = [
                new() { Name = "Gold", Rank = 0, Benefits = "Skip the counter, choose from the Gold aisle" },
                new() { Name = "Five Star", Rank = 1, Benefits = "Free single upgrade, wider car selection" },
                new() { Name = "President's Circle", Rank = 2, Benefits = "Guaranteed upgrade, any car from the President's Circle aisle" }],
            Notes = "Five Star comes free with the IHG Premier card, not from renting.",
        };
        db.LoyaltyPrograms.AddRange(ihgProgram, hiltonProgram, deltaProgram, hertzProgram);

        Card C(string name, string issuer, string network, int statementDay, int dueDay, decimal fee = 0, int? feeMonth = null, LoyaltyProgram? program = null, decimal? centsPerPoint = null) =>
            new()
            {
                Name = name, Issuer = issuer, Network = network, StatementDay = statementDay, DueDay = dueDay, AnnualFee = fee, AnnualFeeMonth = feeMonth,
                PayingAccount = chaseMain, Apr = 24.99m, CreditLimit = 10_000m, LoyaltyProgram = program, PointValueCents = centsPerPoint,
            };

        var chaseIhg = C("Chase IHG One Rewards Premier", "Chase", "Mastercard", 12, 9, 99m, 4, ihgProgram);
        chaseIhg.EarnRules.AddRange([
            new EarnRule { Category = travel, Label = ihgLabel, PointsPerDollar = 10 }, new EarnRule { Category = travel, PointsPerDollar = 5 },
            new EarnRule { Category = gas, PointsPerDollar = 5 }, new EarnRule { Category = restaurants, PointsPerDollar = 5 }, new EarnRule { PointsPerDollar = 3 }]);
        chaseIhg.Thresholds.AddRange([
            new SpendThreshold { Amount = 20_000m, RewardKind = ThresholdRewardKind.Credit, Description = "$100 statement credit + 10,000 points", ValueDollars = 150m },
            new SpendThreshold { Amount = 40_000m, RewardKind = ThresholdRewardKind.Status, Description = "IHG Diamond Elite", TierName = "Diamond" }]);

        chaseIhg.Perks.AddRange([
            new CardPerk { Description = "TSA PreCheck / Global Entry fee credit", AnnualValue = 20m, Notes = "$78 every 4 years, counted per year" },
            new CardPerk { Description = "Fourth night free on award stays", AnnualValue = 150m }]);

        var hiltonSurpass = C("Amex Hilton Honors Surpass", "American Express", "Amex", 20, 15, 150m, 6, hiltonProgram);
        hiltonSurpass.EarnRules.AddRange([
            new EarnRule { Category = travel, Label = hiltonLabel, PointsPerDollar = 12 }, new EarnRule { Category = restaurants, PointsPerDollar = 6 },
            new EarnRule { Category = groceries, PointsPerDollar = 6 }, new EarnRule { Category = gas, PointsPerDollar = 6 }, new EarnRule { PointsPerDollar = 3 }]);
        hiltonSurpass.Thresholds.AddRange([
            new SpendThreshold { Amount = 15_000m, RewardKind = ThresholdRewardKind.FreeNight, Description = "Free night reward", ValueDollars = 250m },
            new SpendThreshold { Amount = 40_000m, RewardKind = ThresholdRewardKind.Status, Description = "Hilton Diamond", TierName = "Diamond" }]);

        var amexDelta = C("Amex Delta SkyMiles Gold", "American Express", "Amex", 8, 5, 150m, 9, deltaProgram);
        amexDelta.EarnRules.AddRange([new EarnRule { Category = travel, PointsPerDollar = 2 }, new EarnRule { Category = restaurants, PointsPerDollar = 2 }, new EarnRule { Category = groceries, PointsPerDollar = 2 }, new EarnRule { PointsPerDollar = 1 }]);

        // Cash-back cards: no program, a point worth exactly 1¢, so "points" come out in dollars.
        var sapphire = C("Chase Sapphire Preferred", "Chase", "Visa", 16, 13, 95m, 3, centsPerPoint: 1.25m);
        sapphire.EarnRules.AddRange([new EarnRule { Category = travel, PointsPerDollar = 2 }, new EarnRule { Category = restaurants, PointsPerDollar = 3 }, new EarnRule { Category = streaming, PointsPerDollar = 3 }, new EarnRule { PointsPerDollar = 1 }]);
        var freedom = C("Chase Freedom Unlimited", "Chase", "Visa", 14, 11, centsPerPoint: 1.0m);
        freedom.EarnRules.AddRange([
            new EarnRule { Category = restaurants, PointsPerDollar = 3 },
            new EarnRule { Category = drugstore, PointsPerDollar = 3 },
            // General merchandise pays 5× at Amazon this year, and the card's base rate at Costco or anywhere else.
            new EarnRule { Category = merchandise, Label = amazon, PointsPerDollar = 5, Notes = "Amazon promo rate", StartYear = 2026, EndYear = 2026 },
            new EarnRule { PointsPerDollar = 1.5m }]);
        var citi = C("Citi Double Cash", "Citi", "Mastercard", 10, 7, centsPerPoint: 1.0m);
        citi.EarnRules.Add(new EarnRule { PointsPerDollar = 2 });

        db.Cards.AddRange(chaseIhg, hiltonSurpass, amexDelta, sapphire, freedom, citi);
        await db.SaveChangesAsync(ct);

        // Status paths: how each tier can be earned, including the card ones.
        ihgProgram.Paths.AddRange([
            new StatusPath { TierName = "Platinum", Kind = StatusPathKind.HoldCard, Card = chaseIhg, Notes = "for holding the IHG Premier" },
            new StatusPath { TierName = "Diamond", Kind = StatusPathKind.CardSpend, Threshold = 40_000m, Card = chaseIhg },
            new StatusPath { TierName = "Diamond", Kind = StatusPathKind.Nights, Threshold = 70 },
            new StatusPath { TierName = "Diamond", Kind = StatusPathKind.QualifyingPoints, Threshold = 120_000 },
            new StatusPath { TierName = "Platinum", Kind = StatusPathKind.Nights, Threshold = 40 }]);
        hiltonProgram.Paths.AddRange([
            new StatusPath { TierName = "Gold", Kind = StatusPathKind.HoldCard, Card = hiltonSurpass, Notes = "for holding the Surpass" },
            new StatusPath { TierName = "Diamond", Kind = StatusPathKind.CardSpend, Threshold = 40_000m, Card = hiltonSurpass },
            new StatusPath { TierName = "Diamond", Kind = StatusPathKind.Nights, Threshold = 50 },
            new StatusPath { TierName = "Diamond", Kind = StatusPathKind.Stays, Threshold = 25 },
            new StatusPath { TierName = "Diamond", Kind = StatusPathKind.ProgramSpend, Threshold = 11_500m, Notes = "2026 eligible Hilton spend path" }]);
        deltaProgram.Paths.Add(new StatusPath { TierName = "Silver", Kind = StatusPathKind.QualifyingPoints, Threshold = 6_000m, Notes = "MQDs" });
        // A card can grant status in a program it has nothing else to do with.
        hertzProgram.Paths.Add(new StatusPath { TierName = "Five Star", Kind = StatusPathKind.HoldCard, Card = chaseIhg, Notes = "benefit of the IHG Premier card" });
        await db.SaveChangesAsync(ct);

        // Year-to-date card spend, as transactions on the cards. The rewards figures are summed from
        // these, so there is no separate card-spend table to keep in step.
        foreach (var m in Enumerable.Range(1, 9))
        {
            Transaction OnCard(Card card, int day, decimal amount, string desc, Category category) => new()
            {
                Card = card, Date = new DateOnly(2026, m, day), Amount = -amount, Description = desc, Merchant = desc,
                Category = category, Origin = TransactionOrigin.Manual, IsManuallyCategorized = true,
                ExternalId = $"seed:card:{card.Name}:{2026}{m:00}:{desc}",
            };
            db.Transactions.AddRange(
                OnCard(chaseIhg, 6, 650m, "Groceries on the IHG card", groceries),
                OnCard(chaseIhg, 11, 240m, "Fuel on the IHG card", gas),
                OnCard(chaseIhg, 17, 500m, "Everything else on the IHG card", other),
                OnCard(hiltonSurpass, 9, 550m, "Dining on the Surpass", restaurants),
                OnCard(hiltonSurpass, 22, 300m, "Travel on the Surpass", travel));
        }
        BudgetLine V(string name, Category category, decimal monthly, Label? label = null) => new()
        {
            Name = name, Category = category, Label = label, Frequency = BudgetFrequency.Variable,
            ProjectedAmount = monthly, PaymentMethod = PaymentMethodKind.Card, PaymentCard = chaseIhg, FundingAccount = chaseMain,
        };

        db.BudgetLines.AddRange(
            new BudgetLine { Name = "Mortgage", Category = housing, DueDay = 1, ProjectedAmount = 2_100m, IsAutopay = true, PaymentMethod = PaymentMethodKind.Account, PaymentAccount = chaseMain, FundingAccount = chaseMain, IsCardEligible = false },
            new BudgetLine { Name = "Water", Category = utilities, DueDay = 20, ProjectedAmount = 60m, IsAutopay = true, PaymentAccount = automatedBills, FundingAccount = automatedBills },
            new BudgetLine { Name = "Sewer", Category = utilities, DueDay = 20, ProjectedAmount = 45m, IsAutopay = true, PaymentAccount = automatedBills, FundingAccount = automatedBills },
            new BudgetLine { Name = "Electric", Category = utilities, DueDay = 18, ProjectedAmount = 140m, IsAutopay = true, PaymentAccount = automatedBills, FundingAccount = automatedBills },
            new BudgetLine { Name = "AT&T", Category = utilities, DueDay = 6, ProjectedAmount = 85m, IsAutopay = true, PaymentAccount = automatedBills, FundingAccount = automatedBills, BankAutopayDiscount = 5m, Notes = "$5/mo discount for bank autopay vs. card" },
            new BudgetLine { Name = "Hulu", Category = subscriptions, DueDay = 11, ProjectedAmount = 18.99m, IsAutopay = true, PaymentMethod = PaymentMethodKind.Card, PaymentCard = chaseIhg, FundingAccount = automatedBills },
            new BudgetLine { Name = "Car insurance", Category = insurance, Frequency = BudgetFrequency.SemiAnnual, DueDay = 15, AnchorDueDate = new(2026, 3, 15), ProjectedAmount = 612m, PaymentMethod = PaymentMethodKind.Card, PaymentCard = hiltonSurpass, FundingAccount = premierSavings },
            new BudgetLine { Name = "AAA", Category = memberships, Frequency = BudgetFrequency.Annual, DueDay = 1, AnchorDueDate = new(2026, 5, 1), ProjectedAmount = 120m, PaymentMethod = PaymentMethodKind.Card, PaymentCard = chaseIhg, FundingAccount = premierSavings },
            new BudgetLine { Name = "Costco", Category = memberships, Frequency = BudgetFrequency.Annual, DueDay = 1, AnchorDueDate = new(2026, 11, 1), ProjectedAmount = 65m, PaymentMethod = PaymentMethodKind.Card, PaymentCard = chaseIhg, FundingAccount = premierSavings },

            // Variable lines: money you plan to spend that has no due date and no biller. Same record as a
            // bill, so the rewards plan draws the whole pool from one place.
            V("Groceries", groceries, 700m), V("Restaurants", restaurants, 600m), V("Gas", gas, 250m),
            V("Travel", travel, 400m), V("Other spending", other, 800m),
            // Amazon is carved out of General merchandise so it can chase its own earn rate.
            V("Amazon", merchandise, 300m, amazon), V("General merchandise", merchandise, 200m));

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
        db.Loans.Add(new Loan
        {
            Name = "Home equity loan", Kind = LoanKind.Heloc, Lender = "Placeholder Bank", OriginalPrincipal = 40_000m,
            AnnualRate = 0.0789m, TermMonths = 120, StartDate = new(2024, 4, 1), ScheduledPayment = 484.31m,
            Notes = "Kitchen and roof",
            Balances = [new LoanBalance { AsOf = new(2026, 9, 1), Balance = 31_400m }],
        });

        db.CategoryRules.AddRange(
            new CategoryRule { Pattern = "AMAZON", CategoryId = merchandise.Id, LabelId = amazon.Id, Priority = 5 },
            new CategoryRule { Pattern = "AMZN", CategoryId = merchandise.Id, LabelId = amazon.Id, Priority = 5 },
            new CategoryRule { Pattern = "COSTCO", CategoryId = merchandise.Id, LabelId = costco.Id, Priority = 5 },
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
        // Monthly valuations, because a house moves with the market and the point is to see that.
        // Roughly 4% annual drift with a soft patch in the spring.
        var houseMoves = new[] { 0m, 900m, 1_400m, 600m, -700m, -1_100m, 400m, 1_500m, 1_800m, 1_200m, 900m, 700m,
                                 500m, 1_300m, 1_600m, 900m, -400m, -900m, 800m, 1_700m, 2_100m };
        var house = new Asset { Name = "House", Kind = AssetKind.Home, Notes = "Bought 2022" };
        var houseValue = 356_000m;
        for (var i = 0; i < houseMoves.Length; i++)
        {
            houseValue += houseMoves[i];
            var month = new DateOnly(2025, 1, 1).AddMonths(i);
            house.Values.Add(new AssetValue { AsOf = month, Value = houseValue });
        }
        db.Assets.Add(house);

        // A car depreciates instead, recorded a couple of times a year.
        db.Assets.Add(new Asset
        {
            Name = "Car", Kind = AssetKind.Vehicle,
            Values = [new AssetValue { AsOf = new(2025, 1, 1), Value = 24_200m },
                      new AssetValue { AsOf = new(2025, 7, 1), Value = 22_100m },
                      new AssetValue { AsOf = new(2026, 1, 1), Value = 20_400m },
                      new AssetValue { AsOf = new(2026, 9, 1), Value = 18_500m }],
        });
        await db.SaveChangesAsync(ct);

        // Both loans are secured against the house, which is what makes equity worth showing.
        foreach (var loan in db.Loans.Local.Where(l => l.Kind is LoanKind.Mortgage or LoanKind.Heloc))
            loan.Asset = house;
        var amexLoanLine = new BudgetLine { Name = "Amex loan", Category = housing, DueDay = 5, ProjectedAmount = 250m, IsAutopay = true, PaymentAccount = chaseMain, FundingAccount = chaseMain, IsCardEligible = false, Notes = "Sam repays 100% each month" };
        db.BudgetLines.Add(amexLoanLine);
        db.People.AddRange(
            new Person
            {
                Name = "Sam",
                Obligations = [new Obligation { Description = "Amex loan payment", BudgetLine = amexLoanLine, ShareOfLine = 1.0m, StartPeriod = new(2026, 1, 1) }],
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

    /// <summary>August and September bank activity: paychecks, lines, a few purchases, and the monthly transfer to the lines account entered by hand and reconciled with the bank's line.</summary>
    private static async Task SeedTransactionsAsync(BudgetDbContext db, Account chaseMain, Account automatedBills, CancellationToken ct)
    {
        var cats = await db.Categories.ToDictionaryAsync(c => c.Name, ct);
        var budgetLines = await db.BudgetLines.ToDictionaryAsync(b => b.Name, ct);
        var batch = new ImportBatch { FileName = "seed-chase.csv", Format = ImportFormat.Csv, Profile = "chase-checking", ImportedAt = DateTime.Now, Account = chaseMain, RowCount = 0 };
        db.ImportBatches.Add(batch);

        Transaction Imported(Account acct, DateOnly date, decimal amount, string desc, string merchant, string? category = null, string? budgetLine = null, bool transfer = false) => new()
        {
            Account = acct, Date = date, Amount = amount, Description = desc, Merchant = merchant, Origin = TransactionOrigin.Imported, ImportBatch = batch,
            CategoryId = category is not null ? cats[category].Id : budgetLine is not null ? budgetLines[budgetLine].CategoryId : null, BudgetLineId = budgetLine is not null ? budgetLines[budgetLine].Id : null,
            IsTransfer = transfer, ExternalId = $"seed:{acct.Name}:{date:yyyyMMdd}:{amount}:{desc}",
        };

        var lines = new List<Transaction>();
        foreach (var (y, m) in new[] { (2026, 8), (2026, 9) })
        {
            var actual = m == 8 ? (Electric: 149.50m, Water: 61.00m) : (Electric: 151.20m, Water: 58.40m);
            lines.AddRange(
            [
                Imported(chaseMain, new(y, m, 1), -2_100m, "MORTGAGE PMT PLACEHOLDER BANK", "Placeholder Bank", budgetLine: "Mortgage"),
                Imported(chaseMain, new(y, m, 4), 4_222.07m, "RIDGELINE PARTNERS PAYROLL", "Ridgeline Partners Payroll"),
                Imported(chaseMain, new(y, m, 18), 4_222.07m, "RIDGELINE PARTNERS PAYROLL", "Ridgeline Partners Payroll"),
                Imported(chaseMain, new(y, m, 5), -250m, "AMEX LOAN PAYMENT", "Amex Loan", budgetLine: "Amex loan"),
                Imported(chaseMain, new(y, m, 9), -84.12m, "KROGER #0456", "Kroger", "Groceries"),
                Imported(chaseMain, new(y, m, 13), -32.10m, "TST* PAPPYS SMOKEHOUSE ST LOUIS MO", "Pappys Smokehouse", "Restaurants"),
                Imported(chaseMain, new(y, m, 21), -46.75m, "SHELL OIL 57444 ST LOUIS MO", "Shell Oil", "Gas"),
                Imported(chaseMain, new(y, m, 2), -330m, "Online Transfer to CHK ...4412", "Online Transfer", transfer: true),
                Imported(automatedBills, new(y, m, 2), 330m, "Online Transfer from CHK ...9901", "Online Transfer", transfer: true),
                Imported(automatedBills, new(y, m, 6), -85m, "ATT PAYMENT", "Att Payment", budgetLine: "AT&T"),
                Imported(automatedBills, new(y, m, 18), -actual.Electric, "AMEREN MISSOURI", "Ameren Missouri", budgetLine: "Electric"),
                Imported(automatedBills, new(y, m, 20), -actual.Water, "CITY WATER UTILITY", "City Water Utility", budgetLine: "Water"),
                Imported(automatedBills, new(y, m, 20), -45m, "MSD SEWER", "Msd Sewer", budgetLine: "Sewer"),
            ]);
            // The same transfer, entered by hand on the 1st (two-sided) and reconciled with the bank's lines on the 2nd.
            var outRow = new Transaction { Account = chaseMain, Date = new(y, m, 1), Amount = -330m, Description = "Transfer out to Chase Automated Bills", Merchant = "Transfer out to Chase Automated Bills", Origin = TransactionOrigin.Manual, IsTransfer = true, IsManuallyCategorized = true, ExternalId = $"manual:seed:{y}{m}:out", CounterpartyAccount = automatedBills, Notes = "monthly budget lines" };
            var inRow = new Transaction { Account = automatedBills, Date = new(y, m, 1), Amount = 330m, Description = "Transfer in from Chase Main", Merchant = "Transfer in from Chase Main", Origin = TransactionOrigin.Manual, IsTransfer = true, IsManuallyCategorized = true, ExternalId = $"manual:seed:{y}{m}:in", CounterpartyAccount = chaseMain, Notes = "monthly budget lines" };
            lines.AddRange([outRow, inRow]);
            // Budget-line actuals those transactions represent.
            foreach (var (name, amt) in new[] { ("Mortgage", 2_100m), ("Amex loan", 250m), ("AT&T", 85m), ("Electric", actual.Electric), ("Water", actual.Water), ("Sewer", 45m) })
                db.BudgetPeriods.Add(new BudgetPeriod { BudgetLineId = budgetLines[name].Id, Period = new(y, m, 1), ActualAmount = amt, Notes = "from import" });
        }
        db.Transactions.AddRange(lines);
        batch.RowCount = lines.Count(l => l.Origin == TransactionOrigin.Imported); batch.ImportedCount = batch.RowCount;

        // A full prior year of actuals, so the Budget hovers have something to compare against.
        // Utilities wander with the season; the fixed ones don't.
        var seasonal = new[] { 1.28m, 1.24m, 1.05m, 0.88m, 0.82m, 0.95m, 1.18m, 1.22m, 1.06m, 0.86m, 0.90m, 1.14m };
        foreach (var m in Enumerable.Range(1, 12))
        {
            void Prior(string name, decimal amount) =>
                db.BudgetPeriods.Add(new BudgetPeriod { BudgetLineId = budgetLines[name].Id, Period = new(2025, m, 1), ActualAmount = Math.Round(amount, 2), Notes = "2025 actual" });

            Prior("Mortgage", 2_050m);                        // escrow stepped up for 2026
            Prior("Amex loan", 250m);
            Prior("AT&T", m >= 7 ? 85m : 80m);                // mid-year price rise
            Prior("Electric", 138m * seasonal[m - 1]);
            Prior("Water", 57m * (1 + (seasonal[m - 1] - 1) / 3));
            Prior("Sewer", 44m);
            Prior("Hulu", m >= 10 ? 18.99m : 15.99m);
        }
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
