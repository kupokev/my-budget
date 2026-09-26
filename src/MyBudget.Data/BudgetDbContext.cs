using Microsoft.EntityFrameworkCore;
using MyBudget.Domain;

namespace MyBudget.Data;

public sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options) : DbContext(options)
{
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<SalaryRate> SalaryRates => Set<SalaryRate>();
    public DbSet<PaySchedule> PaySchedules => Set<PaySchedule>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountBalance> AccountBalances => Set<AccountBalance>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardBalance> CardBalances => Set<CardBalance>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillPeriod> BillPeriods => Set<BillPeriod>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Money: 2 decimal places everywhere; ignored by the in-memory provider, applied by PostgreSQL.
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
        });
        mb.Entity<SalaryRate>().HasIndex(x => new { x.IncomeSourceId, x.EffectiveDate }).IsUnique();
        mb.Entity<PaySchedule>().HasIndex(x => new { x.IncomeSourceId, x.EffectiveDate }).IsUnique();

        mb.Entity<Account>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.LastFour).HasMaxLength(4);
            e.HasMany(x => x.Balances).WithOne(x => x.Account).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Transfers).WithOne(x => x.Account).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<AccountBalance>().HasIndex(x => new { x.AccountId, x.AsOf }).IsUnique();

        mb.Entity<Card>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.LastFour).HasMaxLength(4);
            e.Property(x => x.Apr).HasPrecision(6, 3);
            e.Property(x => x.PromoApr).HasPrecision(6, 3);
            e.HasOne(x => x.PayingAccount).WithMany().HasForeignKey(x => x.PayingAccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Balances).WithOne(x => x.Card).HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<CardBalance>().HasIndex(x => new { x.CardId, x.AsOf }).IsUnique();

        mb.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60);
            e.HasIndex(x => x.Name).IsUnique();
        });

        mb.Entity<Bill>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.PaymentAccount).WithMany().HasForeignKey(x => x.PaymentAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PaymentCard).WithMany().HasForeignKey(x => x.PaymentCardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FundingAccount).WithMany().HasForeignKey(x => x.FundingAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Periods).WithOne(x => x.Bill).HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<BillPeriod>().HasIndex(x => new { x.BillId, x.Period }).IsUnique();
    }
}
