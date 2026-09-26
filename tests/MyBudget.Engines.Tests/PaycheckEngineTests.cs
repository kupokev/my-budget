using MyBudget.Domain;
using MyBudget.Engines.Paycheck;

namespace MyBudget.Engines.Tests;

/// <summary>
/// Algorithm checks against hand-worked Pub 15-T / Missouri arithmetic. The real-stub checks (within $5)
/// get added when Kevin types in two 2026 stubs; see docs/audit.
/// </summary>
public class PaycheckEngineTests
{
    private static readonly FederalRules Fed2026 = RuleSets.Federal(2026);
    private static readonly MissouriRules Mo2026 = RuleSets.Missouri(2026);
    private static readonly W4Inputs SingleNoAdjustments = new(FederalFilingStatus.SingleOrMarriedFilingSeparately);
    private static readonly MoW4Inputs MoSingle = new(MissouriFilingStatus.Single);

    private static PaycheckInput Salary170k(params DeductionInput[] deductions)
        => new(Gross: Math.Round(170_000m / 26, 2), PeriodsPerYear: 26, deductions, SingleNoAdjustments, MoSingle, Fed2026, Mo2026);

    [Fact]
    public void Federal_percentage_method_single_170k_no_deductions()
    {
        // Annualized 6,538.46 × 26 = 170,000 (within cents); taxable = 170,000 − 16,100 = 153,900.
        // Tax: 12,400 × 10% + 38,000 × 12% + 55,300 × 22% + 48,200 × 24% = 1,240 + 4,560 + 12,166 + 11,568 = 29,534 → ÷ 26 = 1,135.92
        var r = PaycheckEngine.Compute(Salary170k());
        Assert.Equal(6_538.46m, r.Gross);
        Assert.InRange(r.FederalIncomeTax.Amount, 1_135.80m, 1_136.00m);
        Assert.Equal(405.38m, r.SocialSecurity.Amount);     // 6,538.46 × 6.2%
        Assert.Equal(94.81m, r.Medicare.Amount);             // × 1.45%
        // Missouri: taxable 170,000 − 16,100 = 153,900; 0% first 1,313; 2%..4.5% on six 1,313 steps = 1,313 × (2+2.5+3+3.5+4+4.5)% = 255.04; 4.7% over 9,191 → 144,709 × 4.7% = 6,801.32; total 7,056.36 ÷ 26 = 271.40
        Assert.InRange(r.MissouriIncomeTax.Amount, 271.30m, 271.50m);
        Assert.Equal(r.Gross - r.TotalTaxes, r.Net);
        Assert.Contains(r.Steps, s => s.StartsWith("Federal: taxable"));
    }

    [Fact]
    public void Section125_reduces_fica_and_income_tax_but_401k_reduces_only_income_tax()
    {
        var r = PaycheckEngine.Compute(Salary170k(
            new("Medical", DeductionTreatment.PreTaxSection125, AmountPerCheck: 150m),
            new("401(k) 6%", DeductionTreatment.PreTaxRetirement, PercentOfGross: 0.06m),
            new("Roth 401(k) 2%", DeductionTreatment.PostTax, PercentOfGross: 0.02m)));

        Assert.Equal(6_388.46m, r.FicaWages);                          // gross − 150
        Assert.Equal(6_388.46m - 392.31m, r.FederalTaxableWages);      // − 6% of gross (392.31)
        Assert.Equal(396.08m, r.SocialSecurity.Amount);                 // 6,388.46 × 6.2%
        Assert.Contains(r.PreTaxDeductions, l => l.Name == "401(k) 6%" && l.Amount == 392.31m && l.Formula.Contains("6.00%"));
        Assert.Contains(r.PostTaxDeductions, l => l.Name.StartsWith("Roth") && l.Amount == 130.77m);
        Assert.Equal(r.Gross - r.TotalPreTaxDeductions - r.TotalTaxes - r.TotalPostTaxDeductions, r.Net);
    }

    [Fact]
    public void W4_adjustments_change_withholding_in_the_documented_direction()
    {
        var baseline = PaycheckEngine.Compute(Salary170k());
        var withCredits = PaycheckEngine.Compute(Salary170k() with { W4 = SingleNoAdjustments with { DependentCredits = 2_000m } });
        var withExtra = PaycheckEngine.Compute(Salary170k() with { W4 = SingleNoAdjustments with { ExtraWithholding = 50m } });
        var twoJobs = PaycheckEngine.Compute(Salary170k() with { W4 = SingleNoAdjustments with { MultipleJobs = true } });
        var joint = PaycheckEngine.Compute(Salary170k() with { W4 = new(FederalFilingStatus.MarriedFilingJointly) });

        Assert.Equal(baseline.FederalIncomeTax.Amount - Math.Round(2_000m / 26, 2), withCredits.FederalIncomeTax.Amount, 2);
        Assert.Equal(baseline.FederalIncomeTax.Amount + 50m, withExtra.FederalIncomeTax.Amount);
        Assert.True(twoJobs.FederalIncomeTax.Amount > baseline.FederalIncomeTax.Amount);
        Assert.True(joint.FederalIncomeTax.Amount < baseline.FederalIncomeTax.Amount);
    }

    [Fact]
    public void Social_security_stops_at_the_wage_base_and_additional_medicare_starts_at_200k()
    {
        var input = Salary170k() with { Gross = 10_000m, PeriodsPerYear = 24 };
        var nearBase = PaycheckEngine.Compute(input with { YtdSocialSecurityWages = 180_000m, YtdMedicareWages = 180_000m });
        Assert.Equal(279m, nearBase.SocialSecurity.Amount);             // only 4,500 of room × 6.2%
        Assert.Contains("wage base", nearBase.SocialSecurity.Formula);

        var overBase = PaycheckEngine.Compute(input with { YtdSocialSecurityWages = 190_000m, YtdMedicareWages = 195_000m });
        Assert.Equal(0m, overBase.SocialSecurity.Amount);
        Assert.Equal(Math.Round(10_000m * 0.0145m + 5_000m * 0.009m, 2), overBase.Medicare.Amount); // 5,000 over 200k
    }

    [Fact]
    public void Year_run_threads_ytd_fica_and_reports_refund_or_owed()
    {
        var checks = Enumerable.Range(0, 26).Select(i => (new DateOnly(2026, 1, 9).AddDays(14 * i), Salary170k()));
        var year = PaycheckEngine.ComputeYear(checks);
        Assert.Equal(26, year.Count);
        var ssTotal = year.Sum(c => c.Result.SocialSecurity.Amount);
        Assert.InRange(ssTotal, 10_539m, 10_540.5m); // 170,000 × 6.2% = 10,540 (under the 184,500 base)

        var withheld = year.Sum(c => c.Result.FederalIncomeTax.Amount);
        var taxable = year.Sum(c => c.Result.FederalTaxableWages);
        var ye = PaycheckEngine.FederalYearEnd(taxable, withheld, SingleNoAdjustments, Fed2026);
        Assert.InRange(Math.Abs(ye.RefundOrOwed), 0m, 2m); // percentage method lands within rounding when nothing changes mid-year
    }

    [Fact]
    public void Bonus_flat_method_withholds_22_percent_federal_and_MO_flat_rate()
    {
        var context = Salary170k() with { YtdSocialSecurityWages = 100_000m, YtdMedicareWages = 100_000m };
        var bonus = PaycheckEngine.Supplemental(10_000m, ytdSupplemental: 0m, context);
        Assert.Equal(2_200m, bonus.FederalIncomeTax.Amount);
        Assert.Equal(470m, bonus.MissouriIncomeTax.Amount);
        Assert.Equal(620m, bonus.SocialSecurity.Amount);
        Assert.Equal(145m, bonus.Medicare.Amount);
        Assert.Equal(10_000m - 2_200m - 470m - 620m - 145m, bonus.Net);

        var huge = PaycheckEngine.Supplemental(20_000m, ytdSupplemental: 990_000m, context);
        Assert.Equal(Math.Round(10_000m * 0.22m + 10_000m * 0.37m, 2), huge.FederalIncomeTax.Amount);
    }

    [Fact]
    public void Bonus_aggregate_method_is_the_marginal_withholding_on_the_combined_check()
    {
        var regular = Salary170k();
        var agg = PaycheckEngine.SupplementalAggregate(10_000m, regular);
        var combined = PaycheckEngine.Compute(regular with { Gross = regular.Gross + 10_000m });
        var alone = PaycheckEngine.Compute(regular);
        Assert.Equal(combined.FederalIncomeTax.Amount - alone.FederalIncomeTax.Amount, agg.FederalIncomeTax.Amount);
        Assert.True(agg.FederalIncomeTax.Amount > 2_200m); // 24% marginal beats the 22% flat rate at this income
    }

    [Fact]
    public void Missouri_bracket_table_shape_is_seven_steps_then_top_rate()
    {
        var b = RuleSets.MoBrackets(1_313m, 0.047m);
        Assert.Equal(8, b.Count);
        Assert.Equal((0m, 0m), (b[0].Over, b[0].Rate));
        Assert.Equal((1_313m, 0.02m), (b[1].Over, b[1].Rate));
        Assert.Equal((9_191m, 0.047m), (b[7].Over, b[7].Rate));
    }
}
