using MyBudget.Domain;

namespace MyBudget.Engines.Paycheck;

/// <summary>A marginal bracket: <see cref="Rate"/> applies to the amount over <see cref="Over"/> up to the next bracket's Over.</summary>
public sealed record Bracket(decimal Over, decimal Rate);

/// <summary>Federal rules for one year, in the shape the engine wants. Built from the TaxYear/TaxBracket reference rows.</summary>
public sealed record FederalRules(
    int Year,
    IReadOnlyDictionary<FederalFilingStatus, decimal> StandardDeduction,
    IReadOnlyDictionary<FederalFilingStatus, IReadOnlyList<Bracket>> Brackets,
    decimal SocialSecurityRate, decimal SocialSecurityWageBase,
    decimal MedicareRate, decimal AdditionalMedicareRate, decimal AdditionalMedicareThreshold,
    decimal SupplementalRate, decimal SupplementalHighRate, decimal SupplementalHighThreshold);

public sealed record MissouriRules(
    int Year,
    IReadOnlyDictionary<MissouriFilingStatus, decimal> StandardDeduction,
    IReadOnlyList<Bracket> Brackets,
    decimal SupplementalRate);

public sealed record W4Inputs(
    FederalFilingStatus Status, bool MultipleJobs = false,
    decimal DependentCredits = 0, decimal OtherIncome = 0, decimal Deductions = 0, decimal ExtraWithholding = 0);

public sealed record MoW4Inputs(MissouriFilingStatus Status, decimal ExtraWithholding = 0);

/// <summary>A deduction as it applies to one check. Percent is a fraction of gross; exactly one of the two is set.</summary>
public sealed record DeductionInput(string Name, DeductionTreatment Treatment, decimal? AmountPerCheck = null, decimal? PercentOfGross = null);

/// <summary>Everything needed to estimate one regular check.</summary>
public sealed record PaycheckInput(
    decimal Gross,
    int PeriodsPerYear,
    IReadOnlyList<DeductionInput> Deductions,
    W4Inputs W4,
    MoW4Inputs MoW4,
    FederalRules Federal,
    MissouriRules Missouri,
    decimal YtdSocialSecurityWages = 0,
    decimal YtdMedicareWages = 0);

/// <summary>One line of the estimate with the formula that produced it (auditability).</summary>
public sealed record Line(string Name, decimal Amount, string Formula);

public sealed record PaycheckResult(
    decimal Gross,
    IReadOnlyList<Line> PreTaxDeductions,
    decimal FicaWages,
    decimal FederalTaxableWages,
    decimal MissouriTaxableWages,
    Line FederalIncomeTax,
    Line SocialSecurity,
    Line Medicare,
    Line MissouriIncomeTax,
    IReadOnlyList<Line> PostTaxDeductions,
    decimal Net,
    IReadOnlyList<string> Steps)
{
    public decimal TotalPreTaxDeductions => PreTaxDeductions.Sum(l => l.Amount);
    public decimal TotalTaxes => FederalIncomeTax.Amount + SocialSecurity.Amount + Medicare.Amount + MissouriIncomeTax.Amount;
    public decimal TotalPostTaxDeductions => PostTaxDeductions.Sum(l => l.Amount);
}
