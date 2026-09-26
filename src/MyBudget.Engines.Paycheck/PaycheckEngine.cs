using MyBudget.Domain;

namespace MyBudget.Engines.Paycheck;

/// <summary>
/// Estimates one regular check the way an employer's payroll system withholds it, so it lands on the stub:
/// gross → Section 125 → FICA wages → traditional 401(k) → income-tax wages → federal (Pub 15-T
/// percentage method, Worksheet 1A) + Social Security + Medicare + Missouri → post-tax → net.
/// </summary>
public static class PaycheckEngine
{
    public static PaycheckResult Compute(PaycheckInput input)
    {
        var steps = new List<string>();
        var gross = Round(input.Gross);
        steps.Add($"Gross for the period: {gross:C}");

        // 1. Pre-tax deductions, in two classes.
        var preTax = new List<Line>();
        decimal section125 = 0, preTaxRetirement = 0;
        foreach (var d in input.Deductions.Where(d => d.Treatment != DeductionTreatment.PostTax))
        {
            var (amount, formula) = Resolve(d, gross);
            preTax.Add(new Line(d.Name, amount, formula));
            if (d.Treatment == DeductionTreatment.PreTaxSection125) section125 += amount; else preTaxRetirement += amount;
        }
        var ficaWages = Round(gross - section125);
        var taxableWages = Round(ficaWages - preTaxRetirement);
        steps.Add($"FICA wages = gross − Section 125 ({section125:C}) = {ficaWages:C}");
        steps.Add($"Income-tax wages = FICA wages − pre-tax retirement ({preTaxRetirement:C}) = {taxableWages:C}");

        // 2. Federal income tax: Pub 15-T percentage method for automated payroll (Worksheet 1A).
        var fed = FederalIncomeTax(taxableWages, input.PeriodsPerYear, input.W4, input.Federal, steps);

        // 3. FICA with year-to-date wage base tracking.
        var ss = SocialSecurity(ficaWages, input.YtdSocialSecurityWages, input.Federal);
        var med = Medicare(ficaWages, input.YtdMedicareWages, input.Federal);
        steps.Add($"Social Security: {ss.Formula}");
        steps.Add($"Medicare: {med.Formula}");

        // 4. Missouri.
        var mo = MissouriIncomeTax(taxableWages, input.PeriodsPerYear, input.MoW4, input.Missouri, steps);

        // 5. Post-tax deductions and net.
        var postTax = new List<Line>();
        foreach (var d in input.Deductions.Where(d => d.Treatment == DeductionTreatment.PostTax))
        {
            var (amount, formula) = Resolve(d, gross);
            postTax.Add(new Line(d.Name, amount, formula));
        }
        var net = Round(gross - preTax.Sum(l => l.Amount) - fed.Amount - ss.Amount - med.Amount - mo.Amount - postTax.Sum(l => l.Amount));
        steps.Add($"Net = gross − pre-tax ({preTax.Sum(l => l.Amount):C}) − taxes ({fed.Amount + ss.Amount + med.Amount + mo.Amount:C}) − post-tax ({postTax.Sum(l => l.Amount):C}) = {net:C}");

        return new PaycheckResult(gross, preTax, ficaWages, taxableWages, taxableWages, fed, ss, med, mo, postTax, net, steps);
    }

    /// <summary>
    /// Worksheet 1A. The "standard" 15-T table is the annual brackets shifted by the standard deduction, so
    /// annualize, subtract the standard deduction (halved with Step 2 checked, brackets halved too), apply
    /// brackets, subtract Step 3 credits, divide by periods, add Step 4(c).
    /// </summary>
    public static Line FederalIncomeTax(decimal taxableWages, int periods, W4Inputs w4, FederalRules rules, List<string>? steps = null)
    {
        var annual = taxableWages * periods + w4.OtherIncome - w4.Deductions;
        var stdDed = rules.StandardDeduction[w4.Status];
        var brackets = rules.Brackets[w4.Status];
        if (w4.MultipleJobs) { stdDed /= 2; brackets = Brackets.Scale(brackets, 0.5m); }
        var annualTaxable = Math.Max(0, annual - stdDed);
        var (annualTax, trace) = Brackets.Tax(annualTaxable, brackets);
        var afterCredits = Math.Max(0, annualTax - w4.DependentCredits);
        var perPeriod = Round(afterCredits / periods + w4.ExtraWithholding);

        steps?.Add($"Federal: annualized wages = {taxableWages:C} × {periods}{(w4.OtherIncome != 0 ? $" + other income {w4.OtherIncome:C}" : "")}{(w4.Deductions != 0 ? $" − deductions {w4.Deductions:C}" : "")} = {annual:C}");
        steps?.Add($"Federal: taxable = {annual:C} − standard deduction {stdDed:C}{(w4.MultipleJobs ? " (halved, Step 2 checked)" : "")} = {annualTaxable:C}");
        steps?.Add($"Federal: annual tax = {trace}{(w4.DependentCredits != 0 ? $"; less credits {w4.DependentCredits:C} = {afterCredits:N2}" : "")}");
        steps?.Add($"Federal: per check = {afterCredits:N2} ÷ {periods}{(w4.ExtraWithholding != 0 ? $" + extra {w4.ExtraWithholding:C}" : "")} = {perPeriod:C}");

        return new Line("Federal income tax", perPeriod,
            $"(({taxableWages:N2} × {periods}{(w4.OtherIncome != 0 ? $" + {w4.OtherIncome:N2}" : "")}{(w4.Deductions != 0 ? $" − {w4.Deductions:N2}" : "")}) − {stdDed:N2}) → {trace}{(w4.DependentCredits != 0 ? $" − {w4.DependentCredits:N2}" : "")}; ÷ {periods}{(w4.ExtraWithholding != 0 ? $" + {w4.ExtraWithholding:N2}" : "")}");
    }

    public static Line SocialSecurity(decimal ficaWages, decimal ytdSsWages, FederalRules rules)
    {
        var room = Math.Max(0, rules.SocialSecurityWageBase - ytdSsWages);
        var subject = Math.Min(ficaWages, room);
        var amount = Round(subject * rules.SocialSecurityRate);
        var formula = subject == ficaWages
            ? $"{ficaWages:N2} × {rules.SocialSecurityRate:P2}"
            : $"min({ficaWages:N2}, wage base {rules.SocialSecurityWageBase:N0} − YTD {ytdSsWages:N2}) = {subject:N2} × {rules.SocialSecurityRate:P2}";
        return new Line("Social Security", amount, formula);
    }

    public static Line Medicare(decimal ficaWages, decimal ytdMedicareWages, FederalRules rules)
    {
        var basic = ficaWages * rules.MedicareRate;
        var overThreshold = Math.Max(0, ytdMedicareWages + ficaWages - Math.Max(ytdMedicareWages, rules.AdditionalMedicareThreshold));
        var additional = overThreshold * rules.AdditionalMedicareRate;
        var formula = $"{ficaWages:N2} × {rules.MedicareRate:P2}" + (additional > 0 ? $" + {overThreshold:N2} over {rules.AdditionalMedicareThreshold:N0} × {rules.AdditionalMedicareRate:P1}" : "");
        return new Line("Medicare", Round(basic + additional), formula);
    }

    /// <summary>Missouri employer formula: annualize, subtract the MO standard deduction for the MO W-4 status, apply the rate table, divide, add extra.</summary>
    public static Line MissouriIncomeTax(decimal taxableWages, int periods, MoW4Inputs w4, MissouriRules rules, List<string>? steps = null)
    {
        var annual = taxableWages * periods;
        var stdDed = rules.StandardDeduction[w4.Status];
        var annualTaxable = Math.Max(0, annual - stdDed);
        var (annualTax, trace) = Brackets.Tax(annualTaxable, rules.Brackets);
        var perPeriod = Round(annualTax / periods + w4.ExtraWithholding);
        steps?.Add($"Missouri: taxable = {taxableWages:C} × {periods} − standard deduction {stdDed:C} = {annualTaxable:C}; tax {trace}; ÷ {periods}{(w4.ExtraWithholding != 0 ? $" + extra {w4.ExtraWithholding:C}" : "")} = {perPeriod:C}");
        return new Line("Missouri income tax", perPeriod, $"(({taxableWages:N2} × {periods}) − {stdDed:N2}) → {trace}; ÷ {periods}{(w4.ExtraWithholding != 0 ? $" + {w4.ExtraWithholding:N2}" : "")}");
    }

    /// <summary>
    /// A bonus or other one-time supplemental payment (INC-12), flat method: the supplemental rate on the whole
    /// amount (the high rate on any part above the cumulative threshold), Missouri's flat rate, FICA as usual.
    /// Pre-tax deductions do not apply to a bonus check by default; pass any that do.
    /// </summary>
    public static PaycheckResult Supplemental(decimal amount, decimal ytdSupplemental, PaycheckInput context)
    {
        var steps = new List<string>();
        var gross = Round(amount);
        var f = context.Federal;
        var overHigh = Math.Max(0, ytdSupplemental + gross - Math.Max(ytdSupplemental, f.SupplementalHighThreshold));
        var atFlat = gross - overHigh;
        var fedAmount = Round(atFlat * f.SupplementalRate + overHigh * f.SupplementalHighRate);
        var fed = new Line("Federal income tax (supplemental flat rate)", fedAmount,
            $"{atFlat:N2} × {f.SupplementalRate:P0}" + (overHigh > 0 ? $" + {overHigh:N2} over {f.SupplementalHighThreshold:N0} YTD × {f.SupplementalHighRate:P0}" : ""));
        steps.Add($"Supplemental pay {gross:C}: federal flat {fed.Formula} = {fedAmount:C}");

        var ss = SocialSecurity(gross, context.YtdSocialSecurityWages, f);
        var med = Medicare(gross, context.YtdMedicareWages, f);
        var moAmount = Round(gross * context.Missouri.SupplementalRate);
        var mo = new Line("Missouri income tax (supplemental flat rate)", moAmount, $"{gross:N2} × {context.Missouri.SupplementalRate:P1}");
        steps.Add($"Social Security: {ss.Formula}; Medicare: {med.Formula}; Missouri flat: {mo.Formula}");

        var preTax = new List<Line>();
        var postTax = new List<Line>();
        foreach (var d in context.Deductions)
        {
            var (amt, formula) = Resolve(d, gross);
            (d.Treatment == DeductionTreatment.PostTax ? postTax : preTax).Add(new Line(d.Name, amt, formula));
        }
        var net = Round(gross - fedAmount - ss.Amount - med.Amount - moAmount - preTax.Sum(l => l.Amount) - postTax.Sum(l => l.Amount));
        steps.Add($"Net = {gross:C} − taxes − deductions = {net:C}");
        return new PaycheckResult(gross, preTax, gross, gross, gross, fed, ss, med, mo, postTax, net, steps);
    }

    /// <summary>
    /// Aggregate method (INC-12 option): withhold on regular + bonus together as one period's wages, minus what
    /// the regular check alone would withhold. FICA is computed on the bonus alone with YTD tracking.
    /// </summary>
    public static PaycheckResult SupplementalAggregate(decimal amount, PaycheckInput regular)
    {
        var combined = Compute(regular with { Gross = regular.Gross + amount });
        var baseline = Compute(regular);
        var gross = Round(amount);
        var fed = new Line("Federal income tax (aggregate method)", combined.FederalIncomeTax.Amount - baseline.FederalIncomeTax.Amount,
            $"withholding on {regular.Gross + amount:N2} ({combined.FederalIncomeTax.Amount:N2}) − on {regular.Gross:N2} alone ({baseline.FederalIncomeTax.Amount:N2})");
        var mo = new Line("Missouri income tax (aggregate method)", combined.MissouriIncomeTax.Amount - baseline.MissouriIncomeTax.Amount,
            $"{combined.MissouriIncomeTax.Amount:N2} − {baseline.MissouriIncomeTax.Amount:N2}");
        var ss = SocialSecurity(gross, regular.YtdSocialSecurityWages, regular.Federal);
        var med = Medicare(gross, regular.YtdMedicareWages, regular.Federal);
        var net = Round(gross - fed.Amount - mo.Amount - ss.Amount - med.Amount);
        var steps = new List<string>
        {
            $"Aggregate: combined check {regular.Gross + amount:C} withholds {combined.FederalIncomeTax.Amount:C} federal / {combined.MissouriIncomeTax.Amount:C} MO; the regular check alone {baseline.FederalIncomeTax.Amount:C} / {baseline.MissouriIncomeTax.Amount:C}",
            $"Bonus share: federal {fed.Amount:C}, MO {mo.Amount:C}, SS {ss.Amount:C}, Medicare {med.Amount:C} → net {net:C}",
        };
        return new PaycheckResult(gross, [], gross, gross, gross, fed, ss, med, mo, [], net, steps);
    }

    /// <summary>Runs a sequence of checks in date order, threading year-to-date FICA wages through them.</summary>
    public static IReadOnlyList<(DateOnly Date, PaycheckResult Result)> ComputeYear(IEnumerable<(DateOnly Date, PaycheckInput Input)> checks)
    {
        decimal ytdSs = 0, ytdMed = 0;
        var results = new List<(DateOnly, PaycheckResult)>();
        foreach (var (date, input) in checks.OrderBy(c => c.Date))
        {
            var r = Compute(input with { YtdSocialSecurityWages = ytdSs, YtdMedicareWages = ytdMed });
            ytdSs += r.FicaWages;
            ytdMed += r.FicaWages;
            results.Add((date, r));
        }
        return results;
    }

    /// <summary>
    /// Year-end view (INC-9): the annual federal liability on the year's taxable wages versus what was withheld.
    /// Positive means a refund is due; negative means owed. Credits and 4(b) deductions come from the W-4.
    /// </summary>
    public static (decimal Liability, decimal Withheld, decimal RefundOrOwed, string Formula) FederalYearEnd(
        decimal annualTaxableWages, decimal withheld, W4Inputs w4, FederalRules rules)
    {
        var stdDed = rules.StandardDeduction[w4.Status];
        var taxable = Math.Max(0, annualTaxableWages + w4.OtherIncome - w4.Deductions - stdDed);
        var (tax, trace) = Brackets.Tax(taxable, rules.Brackets[w4.Status]);
        var liability = Round(Math.Max(0, tax - w4.DependentCredits));
        return (liability, withheld, Round(withheld - liability),
            $"liability: ({annualTaxableWages:N2}{(w4.OtherIncome != 0 ? $" + {w4.OtherIncome:N2}" : "")}{(w4.Deductions != 0 ? $" − {w4.Deductions:N2}" : "")} − {stdDed:N2}) → {trace}{(w4.DependentCredits != 0 ? $" − credits {w4.DependentCredits:N2}" : "")}; withheld {withheld:N2}");
    }

    private static (decimal Amount, string Formula) Resolve(DeductionInput d, decimal gross)
    {
        if (d.PercentOfGross is { } pct) return (Round(gross * pct), $"{gross:N2} × {pct:P2}");
        return (Round(d.AmountPerCheck ?? 0), $"{d.AmountPerCheck ?? 0:N2} per check");
    }

    internal static decimal Round(decimal d) => Math.Round(d, 2, MidpointRounding.AwayFromZero);
}
