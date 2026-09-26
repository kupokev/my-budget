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

        Card C(string name, string issuer, string network, int statementDay, int dueDay, decimal fee = 0, int? feeMonth = null) =>
            new() { Name = name, Issuer = issuer, Network = network, StatementDay = statementDay, DueDay = dueDay, AnnualFee = fee, AnnualFeeMonth = feeMonth, PayingAccount = chaseMain, Apr = 24.99m, CreditLimit = 10_000m };
        var chaseIhg = C("Chase IHG Premier", "Chase", "Mastercard", 12, 9, 99m, 4);
        var hiltonSurpass = C("Hilton Honors Surpass", "American Express", "Amex", 20, 15, 150m, 6);
        db.Cards.AddRange(
            C("Capital One 1", "Capital One", "Visa", 5, 2), C("Capital One 2", "Capital One", "Mastercard", 8, 5),
            C("Citi", "Citi", "Mastercard", 10, 7), C("Chase Freedom", "Chase", "Visa", 14, 11),
            C("Chase Sapphire", "Chase", "Visa", 16, 13, 95m, 3), chaseIhg, C("Wells Fargo", "Wells Fargo", "Visa", 18, 15),
            C("Discover", "Discover", "Discover", 22, 19), C("AmEx", "American Express", "Amex", 25, 20),
            C("HICV", "Comenity", "Visa", 27, 24), hiltonSurpass);

        var utilities = new Category { Name = "Utilities" };
        var housing = new Category { Name = "Housing" };
        var subscriptions = new Category { Name = "Subscriptions" };
        var insurance = new Category { Name = "Insurance" };
        var memberships = new Category { Name = "Memberships" };
        db.Categories.AddRange(utilities, housing, subscriptions, insurance, memberships,
            new Category { Name = "Restaurants" }, new Category { Name = "Groceries" }, new Category { Name = "Transportation" });

        db.Bills.AddRange(
            new Bill { Name = "Mortgage", Category = housing, DueDay = 1, ProjectedAmount = 2_100m, IsAutopay = true, PaymentMethod = PaymentMethodKind.Account, PaymentAccount = chaseMain, FundingAccount = chaseMain },
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

        db.AccountBalances.AddRange(
            new AccountBalance { Account = pnc, AsOf = new(2026, 9, 1), Balance = 2_450m },
            new AccountBalance { Account = automatedBills, AsOf = new(2026, 9, 1), Balance = 610m },
            new AccountBalance { Account = chaseMain, AsOf = new(2026, 9, 1), Balance = 3_200m });

        await db.SaveChangesAsync(ct);
    }
}
