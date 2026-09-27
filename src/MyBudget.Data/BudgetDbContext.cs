using Microsoft.EntityFrameworkCore;
using MyBudget.Domain;

namespace MyBudget.Data;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<SalaryRate> SalaryRates => Set<SalaryRate>();
    public DbSet<PaySchedule> PaySchedules => Set<PaySchedule>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountBalance> AccountBalances => Set<AccountBalance>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardBalance> CardBalances => Set<CardBalance>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<DeductionElection> DeductionElections => Set<DeductionElection>();
    public DbSet<WithholdingElection> WithholdingElections => Set<WithholdingElection>();
    public DbSet<Paycheck> Paychecks => Set<Paycheck>();
    public DbSet<TaxYear> TaxYears => Set<TaxYear>();
    public DbSet<ContributionLimits> ContributionLimits => Set<ContributionLimits>();
    public DbSet<HsaYear> HsaYears => Set<HsaYear>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoyaltyProgram> LoyaltyPrograms => Set<LoyaltyProgram>();
    public DbSet<EarnRule> EarnRules => Set<EarnRule>();
    public DbSet<SpendThreshold> SpendThresholds => Set<SpendThreshold>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CategoryRule> CategoryRules => Set<CategoryRule>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<DividendPayment> Dividends => Set<DividendPayment>();
    public DbSet<PriceSnapshot> Prices => Set<PriceSnapshot>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<ReceivablePayment> ReceivablePayments => Set<ReceivablePayment>();
    public DbSet<IncomeReceipt> IncomeReceipts => Set<IncomeReceipt>();
    public DbSet<EstimatedTaxPayment> EstimatedTaxPayments => Set<EstimatedTaxPayment>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Money: 2 decimal places everywhere; ignored by the in-memory provider, applied by SQLite.
        foreach (var property in mb.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(14);
            property.SetScale(2);
        }

        mb.Entity<IncomeSource>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasMany(x => x.SalaryRates).WithOne(x => x.IncomeSource).HasForeignKey(x => x.IncomeSourceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.PaySchedules).WithOne(x => x.IncomeSource).HasForeignKey(x => x.IncomeSourceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Deductions).WithOne(x => x.IncomeSource).HasForeignKey(x => x.IncomeSourceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Withholdings).WithOne(x => x.IncomeSource).HasForeignKey(x => x.IncomeSourceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Overrides).WithOne(x => x.IncomeSource).HasForeignKey(x => x.IncomeSourceId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<PaycheckOverride>(e =>
        {
            e.Property(x => x.GrossFraction).HasPrecision(6, 4);
            e.HasIndex(x => new { x.IncomeSourceId, x.PayDate }).IsUnique();
        });
        mb.Entity<DeductionElection>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.PercentOfGross).HasPrecision(8, 5);
        });
        mb.Entity<WithholdingElection>().HasIndex(x => new { x.IncomeSourceId, x.EffectiveDate }).IsUnique();
        mb.Entity<Paycheck>(e =>
        {
            e.HasOne(x => x.IncomeSource).WithMany().HasForeignKey(x => x.IncomeSourceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Lines).WithOne(x => x.Paycheck).HasForeignKey(x => x.PaycheckId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.IncomeSourceId, x.PayDate, x.Kind }).IsUnique();
        });
        mb.Entity<PaycheckLine>().Property(x => x.Name).HasMaxLength(100);

        mb.Entity<TaxYear>(e =>
        {
            e.HasIndex(x => x.Year).IsUnique();
            e.HasMany(x => x.Brackets).WithOne(x => x.TaxYear).HasForeignKey(x => x.TaxYearId).OnDelete(DeleteBehavior.Cascade);
            foreach (var rate in new[] { nameof(TaxYear.SocialSecurityRate), nameof(TaxYear.MedicareRate), nameof(TaxYear.AdditionalMedicareRate),
                         nameof(TaxYear.SupplementalRate), nameof(TaxYear.SupplementalHighRate), nameof(TaxYear.MissouriSupplementalRate) })
                e.Property(rate).HasPrecision(8, 5);
        });
        mb.Entity<TaxBracket>().Property(x => x.Rate).HasPrecision(8, 5);
        mb.Entity<ContributionLimits>().HasIndex(x => x.Year).IsUnique();

        mb.Entity<HsaYear>(e =>
        {
            e.HasIndex(x => x.Year).IsUnique();
            e.HasMany(x => x.Months).WithOne(x => x.HsaYear).HasForeignKey(x => x.HsaYearId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Contributions).WithOne(x => x.HsaYear).HasForeignKey(x => x.HsaYearId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<HsaMonth>().HasIndex(x => new { x.HsaYearId, x.Month }).IsUnique();
        mb.Entity<HsaContribution>().HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Loan>(e =>
        {
            e.HasOne(x => x.Asset).WithMany(x => x.Loans).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.AccountNumber).HasMaxLength(60);
            e.Property(x => x.AnnualRate).HasPrecision(8, 5);
            e.HasOne(x => x.BudgetLine).WithMany().HasForeignKey(x => x.BudgetLineId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Balances).WithOne(x => x.Loan).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<LoanBalance>().HasIndex(x => new { x.LoanId, x.AsOf }).IsUnique();
        mb.Entity<SalaryRate>().HasIndex(x => new { x.IncomeSourceId, x.EffectiveDate }).IsUnique();
        mb.Entity<PaySchedule>().HasIndex(x => new { x.IncomeSourceId, x.EffectiveDate }).IsUnique();

        mb.Entity<Account>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.AccountNumber).HasMaxLength(60);
            e.HasMany(x => x.Balances).WithOne(x => x.Account).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Transactions).WithOne(x => x.Account).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<AccountBalance>().HasIndex(x => new { x.AccountId, x.AsOf }).IsUnique();

        mb.Entity<Card>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.AccountNumber).HasMaxLength(60);
            e.Property(x => x.Apr).HasPrecision(6, 3);
            e.Property(x => x.PromoApr).HasPrecision(6, 3);
            e.HasOne(x => x.PayingAccount).WithMany().HasForeignKey(x => x.PayingAccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Balances).WithOne(x => x.Card).HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.LoyaltyProgram).WithMany().HasForeignKey(x => x.LoyaltyProgramId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.PointValueCents).HasPrecision(8, 4);
            e.HasMany(x => x.EarnRules).WithOne(x => x.Card).HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Thresholds).WithOne(x => x.Card).HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Perks).WithOne(x => x.Card).HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<EarnRule>(e =>
        {
            e.Property(x => x.PointsPerDollar).HasPrecision(8, 3);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Label).WithMany().HasForeignKey(x => x.LabelId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<SpendThreshold>().Property(x => x.Description).HasMaxLength(200);
        mb.Entity<CardPerk>().Property(x => x.Description).HasMaxLength(200);
        mb.Entity<LoyaltyTier>().Property(x => x.Benefits).HasMaxLength(500);
        mb.Entity<LoyaltyProgram>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.PointValueCents).HasPrecision(8, 4);
            e.HasMany(x => x.Tiers).WithOne(x => x.Program).HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Paths).WithOne(x => x.Program).HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Progress).WithOne(x => x.Program).HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<StatusPath>().HasOne(x => x.Card).WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<LoyaltyProgress>().HasIndex(x => new { x.ProgramId, x.Year }).IsUnique();

        mb.Entity<ImportBatch>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.Profile).HasMaxLength(60);
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<Transaction>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(400);
            e.Property(x => x.Merchant).HasMaxLength(120);
            e.Property(x => x.ExternalId).HasMaxLength(120);
            e.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CounterpartyAccount).WithMany().HasForeignKey(x => x.CounterpartyAccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ReconciledWith).WithMany().HasForeignKey(x => x.ReconciledWithId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ReceivablePayment).WithMany().HasForeignKey(x => x.ReceivablePaymentId).OnDelete(DeleteBehavior.SetNull);
            e.Ignore(x => x.Counts);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Label).WithMany().HasForeignKey(x => x.LabelId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.BudgetLine).WithMany().HasForeignKey(x => x.BudgetLineId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ImportBatch).WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.AccountId, x.CardId, x.ExternalId }).IsUnique();
            e.HasIndex(x => x.Date);
        });
        mb.Entity<CategoryRule>(e =>
        {
            e.Property(x => x.Pattern).HasMaxLength(200);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Label).WithMany().HasForeignKey(x => x.LabelId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.BudgetLine).WithMany().HasForeignKey(x => x.BudgetLineId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<Goal>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.AccountIds).HasMaxLength(200);
        });

        mb.Entity<Holding>(e =>
        {
            e.Property(x => x.Ticker).HasMaxLength(12);
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Trades).WithOne(x => x.Holding).HasForeignKey(x => x.HoldingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Dividends).WithOne(x => x.Holding).HasForeignKey(x => x.HoldingId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AccountId, x.Ticker }).IsUnique();
        });
        mb.Entity<Trade>(e =>
        {
            e.Property(x => x.Shares).HasPrecision(18, 6);
            e.Property(x => x.Price).HasPrecision(18, 4);
            e.HasOne(x => x.DividendPayment).WithMany().HasForeignKey(x => x.DividendPaymentId).OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<DividendPayment>(e =>
        {
            e.Property(x => x.PerShare).HasPrecision(18, 6);
            e.Property(x => x.SharesHeld).HasPrecision(18, 6);
            e.HasIndex(x => new { x.HoldingId, x.ExDate }).IsUnique();
        });
        mb.Entity<PriceSnapshot>(e =>
        {
            e.Property(x => x.Ticker).HasMaxLength(12);
            e.Property(x => x.Price).HasPrecision(18, 4);
            e.HasIndex(x => new { x.Ticker, x.Date }).IsUnique();
        });
        mb.Entity<Asset>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasMany(x => x.Values).WithOne(x => x.Asset).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<AssetValue>().HasIndex(x => new { x.AssetId, x.AsOf }).IsUnique();

        mb.Entity<Person>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasMany(x => x.Obligations).WithOne(x => x.Person).HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Charges).WithOne(x => x.Person).HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne(x => x.Person).HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<Obligation>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.ShareOfLine).HasPrecision(6, 4);
            e.HasOne(x => x.BudgetLine).WithMany().HasForeignKey(x => x.BudgetLineId).OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<ReceivableCharge>().Property(x => x.Description).HasMaxLength(200);
        mb.Entity<ReceivablePayment>().HasMany(x => x.Allocations).WithOne(x => x.Payment).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<IncomeReceipt>().HasOne(x => x.IncomeSource).WithMany().HasForeignKey(x => x.IncomeSourceId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<CardBalance>().HasIndex(x => new { x.CardId, x.AsOf }).IsUnique();

        mb.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60);
            e.HasIndex(x => x.Name).IsUnique();
        });
        mb.Entity<Label>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<BudgetLine>(e =>
        {
            e.HasOne(x => x.Label).WithMany().HasForeignKey(x => x.LabelId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.AccountNumber).HasMaxLength(60);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.PaymentAccount).WithMany().HasForeignKey(x => x.PaymentAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PaymentCard).WithMany().HasForeignKey(x => x.PaymentCardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FundingAccount).WithMany().HasForeignKey(x => x.FundingAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Periods).WithOne(x => x.BudgetLine).HasForeignKey(x => x.BudgetLineId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<BudgetPeriod>().HasIndex(x => new { x.BudgetLineId, x.Period }).IsUnique();
    }
}
