using System.ComponentModel.DataAnnotations;
using MyBudget.Domain;

namespace MyBudget.Contracts;

// ---- Reference tables ----------------------------------------------------------------------

public sealed class TaxBracketDto
{
    public Jurisdiction Jurisdiction { get; set; }
    public FederalFilingStatus? FilingStatus { get; set; }
    public decimal Over { get; set; }
    /// <summary>Percent as a whole number for editing (22 = 22%).</summary>
    public decimal RatePercent { get; set; }
}

/// <summary>Rates are percent numbers here (6.2, not 0.062) so the edit page reads like the published tables.</summary>
public sealed class TaxYearDto
{
    public int Year { get; set; }
    public decimal FederalStandardDeductionSingle { get; set; }
    public decimal FederalStandardDeductionMarriedJointly { get; set; }
    public decimal FederalStandardDeductionHeadOfHousehold { get; set; }
    public decimal SocialSecurityRatePercent { get; set; }
    public decimal SocialSecurityWageBase { get; set; }
    public decimal MedicareRatePercent { get; set; }
    public decimal AdditionalMedicareRatePercent { get; set; }
    public decimal AdditionalMedicareThreshold { get; set; }
    public decimal SupplementalRatePercent { get; set; }
    public decimal SupplementalHighRatePercent { get; set; }
    public decimal SupplementalHighThreshold { get; set; }
    public decimal MissouriStandardDeductionSingle { get; set; }
    public decimal MissouriStandardDeductionMarriedSpouseWorks { get; set; }
    public decimal MissouriStandardDeductionMarriedOneIncome { get; set; }
    public decimal MissouriStandardDeductionHeadOfHousehold { get; set; }
    public decimal MissouriSupplementalRatePercent { get; set; }
    public decimal LtcgThreshold15Single { get; set; }
    public decimal LtcgThreshold15MarriedJointly { get; set; }
    public decimal LtcgThreshold15HeadOfHousehold { get; set; }
    public decimal LtcgThreshold20Single { get; set; }
    public decimal LtcgThreshold20MarriedJointly { get; set; }
    public decimal LtcgThreshold20HeadOfHousehold { get; set; }
    public string? Source { get; set; }
    public bool Verified { get; set; }
    public string? Notes { get; set; }
    public List<TaxBracketDto> Brackets { get; set; } = [];
}

public sealed class ContributionLimitsDto
{
    public int Year { get; set; }
    public decimal HsaSelfOnly { get; set; }
    public decimal HsaFamily { get; set; }
    public decimal HsaCatchUp { get; set; }
    public decimal Retirement401kEmployee { get; set; }
    public decimal Retirement401kCatchUp { get; set; }
    public decimal Retirement401kTotal { get; set; }
    public decimal Ira { get; set; }
    public decimal IraCatchUp { get; set; }
    public string? Source { get; set; }
    public bool Verified { get; set; }
}

public sealed record ReferenceYearSummaryDto(int Year, bool TaxVerified, bool LimitsVerified, string? Notes);

// ---- Paycheck ------------------------------------------------------------------------------

public sealed record LineDto(string Name, decimal Amount, string Formula);

public sealed record PaycheckEstimateDto(
    DateOnly PayDate, string IncomeSource, int PeriodsPerYear, decimal AnnualSalary, int TaxYear, bool TaxYearVerified,
    string W4Summary, string MoW4Summary,
    decimal Gross,
    IReadOnlyList<LineDto> PreTaxDeductions, decimal FicaWages, decimal TaxableWages,
    LineDto FederalIncomeTax, LineDto SocialSecurity, LineDto Medicare, LineDto MissouriIncomeTax,
    IReadOnlyList<LineDto> PostTaxDeductions,
    decimal TotalPreTax, decimal TotalTaxes, decimal TotalPostTax, decimal Net,
    IReadOnlyList<string> Steps, IReadOnlyList<string> Warnings,
    /// <summary>The stretch of work this cheque is for, and whether it is live pay or in arrears.</summary>
    DateOnly? WorkPeriodStart = null, DateOnly? WorkPeriodEnd = null, int PayLagDays = 0,
    /// <summary>How the net splits across deposit accounts, when a split is configured.</summary>
    IReadOnlyList<DepositDto>? Deposits = null);

/// <summary>One account's share of a cheque's net.</summary>
public sealed record DepositDto(int AccountId, string Account, decimal Amount, string Formula,
    /// <summary>What actually landed in that account near this pay date, from transactions tagged to the income source.</summary>
    decimal Received = 0m, int MatchedCount = 0);

public sealed record YearCheckDto(DateOnly Date, decimal Gross, decimal PreTax, decimal Federal, decimal SocialSecurity, decimal Medicare, decimal Missouri, decimal PostTax, decimal Net);

public sealed record YearEstimateDto(
    int Year, string IncomeSource, IReadOnlyList<YearCheckDto> Checks,
    decimal Gross, decimal PreTax, decimal Federal, decimal SocialSecurity, decimal Medicare, decimal Missouri, decimal PostTax, decimal Net,
    decimal FederalLiability, decimal FederalWithheld, decimal FederalRefundOrOwed, string FederalYearEndFormula,
    IReadOnlyList<string> Warnings);

/// <summary>What-if (INC-9): every field null means "as configured". Percent fields are whole numbers.</summary>
public sealed class WhatIfRequest
{
    public int IncomeSourceId { get; set; }
    public DateOnly PayDate { get; set; }
    public decimal? AnnualSalary { get; set; }
    public decimal? Retirement401kPercent { get; set; }
    public FederalFilingStatus? FederalStatus { get; set; }
    public bool? MultipleJobs { get; set; }
    public decimal? DependentCredits { get; set; }
    public decimal? OtherIncome { get; set; }
    public decimal? Deductions { get; set; }
    public decimal? ExtraWithholding { get; set; }
    public MissouriFilingStatus? MissouriStatus { get; set; }
    public decimal? MissouriExtraWithholding { get; set; }
    /// <summary>Replace a named benefit's per-check amount (e.g. Medical → 210).</summary>
    public Dictionary<string, decimal> BenefitAmounts { get; set; } = [];
}

public sealed record WhatIfResponse(PaycheckEstimateDto Baseline, PaycheckEstimateDto Scenario, YearEstimateDto BaselineYear, YearEstimateDto ScenarioYear);

public enum SupplementalMethod { Flat, Aggregate }

public sealed class SupplementalRequest
{
    public int IncomeSourceId { get; set; }
    public DateOnly PayDate { get; set; }
    [Range(0.01, 100_000_000)] public decimal Amount { get; set; }
    public SupplementalMethod Method { get; set; } = SupplementalMethod.Flat;
    /// <summary>Supplemental wages already paid this year, for the $1M high-rate threshold.</summary>
    public decimal YtdSupplemental { get; set; }
}

// ---- Actual stubs (INC-7) ------------------------------------------------------------------

public sealed class PaycheckLineDto
{
    public PaycheckLineCategory Category { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public decimal Amount { get; set; }
}

public sealed class PaycheckDto
{
    public int Id { get; set; }
    public int IncomeSourceId { get; set; }
    public DateOnly PayDate { get; set; }
    public PaycheckKind Kind { get; set; }
    public decimal Gross { get; set; }
    public decimal Net { get; set; }
    public string? Notes { get; set; }
    public List<PaycheckLineDto> Lines { get; set; } = [];
}

public sealed record CompareRowDto(string Name, decimal? Estimated, decimal? Actual, decimal? Variance);

public sealed record PaycheckCompareDto(PaycheckDto Actual, PaycheckEstimateDto Estimate, IReadOnlyList<CompareRowDto> Rows, decimal NetVariance, bool WithinFiveDollars);

// ---- HSA -----------------------------------------------------------------------------------

public sealed class HsaContributionDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    [Range(0.01, 1_000_000)] public decimal Amount { get; set; }
    public HsaContributionSource Source { get; set; }
    public int? AccountId { get; set; }
    public string? Notes { get; set; }
}

public sealed class HsaYearDto
{
    public int Year { get; set; }
    public decimal? TargetAmount { get; set; }
    public DateOnly? TargetDate { get; set; }
    public bool CatchUpEligible { get; set; }
    public decimal? LimitOverrideSelfOnly { get; set; }
    public decimal? LimitOverrideFamily { get; set; }
    public string? Notes { get; set; }
    /// <summary>Twelve entries, January first.</summary>
    public List<HsaTier> Months { get; set; } = Enumerable.Repeat(HsaTier.NotEligible, 12).ToList();
    public List<HsaContributionDto> Contributions { get; set; } = [];
}

public sealed record HsaPlanDto(
    int Year, DateOnly AsOf,
    decimal LimitSelfOnly, decimal LimitFamily, decimal LimitCatchUp, string LimitsSource,
    int EligibleMonths, decimal AnnualLimit, string LimitFormula,
    decimal Employer, decimal Payroll, decimal Direct, decimal Contributed, decimal Room,
    decimal Target, string TargetSource, decimal RemainingToTarget,
    int MonthsLeft, decimal RecommendedMonthly, int PaychecksLeft, decimal RecommendedPerPaycheck, string PaychecksSource,
    bool OverContributed, decimal OverBy, IReadOnlyList<string> Steps);

// ---- Loans ---------------------------------------------------------------------------------

public sealed class LoanDto
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public LoanKind Kind { get; set; }
    [StringLength(100)] public string? Lender { get; set; }
    [StringLength(60)] public string? AccountNumber { get; set; }
    [Range(0, 100_000_000)] public decimal OriginalPrincipal { get; set; }
    /// <summary>Percent as a whole number (6.5 = 6.5%).</summary>
    [Range(0, 100)] public decimal AnnualRatePercent { get; set; }
    [Range(1, 600)] public int TermMonths { get; set; } = 360;
    public DateOnly StartDate { get; set; }
    public decimal? ScheduledPayment { get; set; }
    [Range(0, 1_000_000)] public decimal ExtraMonthlyPayment { get; set; }
    public int? BudgetLineId { get; set; }
    /// <summary>The asset this loan is secured against, if any.</summary>
    public int? AssetId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? LatestBalance { get; set; }
    public DateOnly? LatestBalanceAsOf { get; set; }

    /// <summary>
    /// The balance to show. Falls back to the original principal when nothing has been recorded yet,
    /// with <see cref="BalanceIsEstimate"/> set so a screen can say so rather than implying it is known.
    /// </summary>
    public decimal EffectiveBalance { get; set; }

    /// <summary>True when no balance has been recorded and the original principal is standing in.</summary>
    public bool BalanceIsEstimate { get; set; }
}

public sealed class LoanBalanceDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public DateOnly AsOf { get; set; }
    public decimal Balance { get; set; }
}

public sealed record ScheduleRowDto(int Number, DateOnly Date, decimal Payment, decimal Interest, decimal Principal, decimal Extra, decimal Balance);

public sealed record LoanProjectionDto(
    int LoanId, string Name, decimal ScheduledPayment, string PaymentFormula, decimal ExtraMonthly,
    decimal FromBalance, DateOnly FromDate, string FromSource,
    DateOnly? PayoffDate, int MonthsRemaining, decimal InterestRemaining, decimal TotalRemaining,
    decimal? InterestSavedByExtra, int? MonthsSavedByExtra,
    /// <summary>What the original schedule says the balance should be now, to compare against the statement.</summary>
    decimal? ScheduledBalanceNow, int PaymentsMadeOnSchedule,
    IReadOnlyList<ScheduleRowDto> Rows);
