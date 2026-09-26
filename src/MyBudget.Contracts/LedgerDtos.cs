using System.ComponentModel.DataAnnotations;
using MyBudget.Domain;

namespace MyBudget.Contracts;

// One DTO per entity, used for both reading and writing: Id 0 means "create". Kept flat and
// settable so Blazor EditForm can bind straight to it.

public sealed class AccountDto
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(100)] public string? Institution { get; set; }
    public AccountType Type { get; set; }
    [StringLength(4)] public string? LastFour { get; set; }
    [Range(0, 1_000_000)] public decimal MinimumBalance { get; set; }
    public TransferCadence TransferCadence { get; set; } = TransferCadence.Monthly;
    public bool IsRainyDayFund { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Latest known balance, read-only.</summary>
    public decimal? LatestBalance { get; set; }
    public DateOnly? LatestBalanceAsOf { get; set; }
}

public sealed class AccountBalanceDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Balance { get; set; }
}

public sealed class TransferDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class CardDto
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(60)] public string? Issuer { get; set; }
    [StringLength(30)] public string? Network { get; set; }
    [StringLength(4)] public string? LastFour { get; set; }
    [Range(0, 100)] public decimal Apr { get; set; }
    [Range(0, 100)] public decimal? PromoApr { get; set; }
    public DateOnly? PromoAprExpires { get; set; }
    [Range(1, 31)] public int StatementDay { get; set; } = 1;
    [Range(1, 31)] public int DueDay { get; set; } = 1;
    [Range(0, 10_000_000)] public decimal CreditLimit { get; set; }
    [Range(0, 100_000)] public decimal AnnualFee { get; set; }
    [Range(1, 12)] public int? AnnualFeeMonth { get; set; }
    public int? PayingAccountId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? LatestBalance { get; set; }
    public DateOnly? LatestBalanceAsOf { get; set; }
}

public sealed class CardBalanceDto
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Balance { get; set; }
}

public sealed class CategoryDto
{
    public int Id { get; set; }
    [Required, StringLength(60)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class BillDto
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public int? CategoryId { get; set; }
    public BillFrequency Frequency { get; set; } = BillFrequency.Monthly;
    [Range(1, 31)] public int DueDay { get; set; } = 1;
    public DateOnly? AnchorDueDate { get; set; }
    public bool IsAutopay { get; set; }
    [Range(0, 10_000_000)] public decimal ProjectedAmount { get; set; }
    public PaymentMethodKind PaymentMethod { get; set; } = PaymentMethodKind.Account;
    public int? PaymentAccountId { get; set; }
    public int? PaymentCardId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Pick the funding account.")] public int FundingAccountId { get; set; }
    [Range(0, 10_000)] public decimal? BankAutopayDiscount { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BillActualDto
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public DateOnly Period { get; set; }
    public decimal Amount { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? Notes { get; set; }
}

public sealed class IncomeSourceDto
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public IncomeSourceType Type { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public List<SalaryRateDto> SalaryRates { get; set; } = [];
    public List<PayScheduleDto> PaySchedules { get; set; } = [];
}

public sealed class SalaryRateDto
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    [Range(0, 100_000_000)] public decimal AnnualAmount { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

public sealed class PayScheduleDto
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public PayFrequency Frequency { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly AnchorPayDate { get; set; }
    public bool PayOnPriorBusinessDay { get; set; } = true;
    [Range(1, 31)] public int? FirstPayDay { get; set; }
    [Range(1, 31)] public int? SecondPayDay { get; set; }
}
