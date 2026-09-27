using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Ledger;
using MyBudget.Engines.Paycheck;

namespace MyBudget.Api;

/// <summary>Assembles engine inputs from what's in effect on a date (salary, schedule, elections, W-4, tax year) and runs the estimator.</summary>
public sealed class PaycheckService(BudgetDbContext db)
{
    public sealed record Context(IncomeSource Source, DateOnly Date, SalaryRate Salary, PaySchedule Schedule, int PeriodsPerYear,
        List<DeductionElection> Deductions, WithholdingElection W4, TaxYear TaxYear, List<string> Warnings, PaycheckOverride? Override = null);

    public async Task<Context> LoadAsync(int sourceId, DateOnly date)
    {
        var source = await db.IncomeSources.Include(s => s.SalaryRates).Include(s => s.PaySchedules).Include(s => s.Deductions).Include(s => s.Withholdings).Include(s => s.Overrides)
            .FirstOrDefaultAsync(s => s.Id == sourceId) ?? throw new KeyNotFoundException($"Income source {sourceId} not found.");
        if (source.EndDate is { } ended && date > ended) throw new InvalidOperationException($"{source.Name} employment ended {ended:yyyy-MM-dd}; no check on {date:yyyy-MM-dd}.");
        var warnings = new List<string>();
        var ovr = source.Overrides.FirstOrDefault(o => o.PayDate == date);
        if (ovr is not null)
            warnings.Add(ovr.GrossAmount is { } ga ? $"This check is overridden to {ga:C} gross{(ovr.Notes is null ? "" : $" ({ovr.Notes})")}."
                : $"This check is {ovr.GrossFraction:P0} of a normal period{(ovr.ProrateFixedDeductions ? ", fixed deductions prorated too" : ", fixed deductions in full")}{(ovr.Notes is null ? "" : $" ({ovr.Notes})")}.");

        var salary = source.SalaryRates.Where(r => r.EffectiveDate <= date).OrderByDescending(r => r.EffectiveDate).FirstOrDefault()
            ?? throw new InvalidOperationException($"{source.Name} has no salary rate in effect on {date:yyyy-MM-dd}.");
        var schedule = source.PaySchedules.Where(p => p.EffectiveDate <= date).OrderByDescending(p => p.EffectiveDate).FirstOrDefault()
            ?? throw new InvalidOperationException($"{source.Name} has no pay schedule in effect on {date:yyyy-MM-dd}.");
        var deductions = source.Deductions.Where(d => d.EffectiveDate <= date && (d.EndDate is null || d.EndDate >= date)).OrderBy(d => d.Name).ToList();
        var w4 = source.Withholdings.Where(w => w.EffectiveDate <= date).OrderByDescending(w => w.EffectiveDate).FirstOrDefault();
        if (w4 is null)
        {
            warnings.Add("No W-4 on file for this date; assuming Single with no adjustments.");
            w4 = new WithholdingElection { FederalStatus = FederalFilingStatus.SingleOrMarriedFilingSeparately, MissouriStatus = MissouriFilingStatus.Single, EffectiveDate = date };
        }
        var taxYear = await db.TaxYears.Include(t => t.Brackets).FirstOrDefaultAsync(t => t.Year == date.Year);
        if (taxYear is null)
        {
            taxYear = await db.TaxYears.Include(t => t.Brackets).OrderByDescending(t => t.Year).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No tax tables loaded.");
            warnings.Add($"No {date.Year} tax tables; using {taxYear.Year}. Enter {date.Year} under Tax tables.");
        }
        if (!taxYear.Verified) warnings.Add($"{taxYear.Year} tax tables are not marked verified against the published tables.");
        if (!string.IsNullOrWhiteSpace(taxYear.Notes)) warnings.Add(taxYear.Notes);

        return new Context(source, date, salary, schedule, PayDates.PaychecksPerYear(schedule.Frequency), deductions, w4, taxYear, warnings, ovr);
    }

    public static PaycheckInput BuildInput(Context c, WhatIfRequest? whatIf = null)
    {
        var annual = whatIf?.AnnualSalary ?? c.Salary.AnnualAmount;
        var gross = Math.Round(annual / c.PeriodsPerYear, 2);
        var fraction = 1m;
        if (c.Override is { } o)
        {
            if (o.GrossAmount is { } ga) { fraction = gross == 0 ? 1 : ga / gross; gross = ga; }
            else if (o.GrossFraction is { } gf) { fraction = gf; gross = Math.Round(gross * gf, 2); }
        }
        var deductions = c.Deductions.Select(d =>
        {
            var amount = d.AmountPerCheck;
            if (amount is { } fixedAmt && c.Override is { ProrateFixedDeductions: true }) amount = Math.Round(fixedAmt * fraction, 2);
            var pct = d.PercentOfGross;
            if (whatIf is not null && whatIf.BenefitAmounts.TryGetValue(d.Name, out var o)) { amount = o; pct = null; }
            if (whatIf?.Retirement401kPercent is { } k && d.Kind == DeductionKind.Retirement401k) { pct = k / 100m; amount = null; }
            return new DeductionInput(d.Name, d.Treatment, amount, pct);
        }).ToList();
        var w = c.W4;
        var w4 = new W4Inputs(whatIf?.FederalStatus ?? w.FederalStatus, whatIf?.MultipleJobs ?? w.MultipleJobs, whatIf?.DependentCredits ?? w.DependentCredits,
            whatIf?.OtherIncome ?? w.OtherIncome, whatIf?.Deductions ?? w.Deductions, whatIf?.ExtraWithholding ?? w.ExtraWithholding);
        var mo = new MoW4Inputs(whatIf?.MissouriStatus ?? w.MissouriStatus, whatIf?.MissouriExtraWithholding ?? w.MissouriExtraWithholding);
        return new PaycheckInput(gross, c.PeriodsPerYear, deductions, w4, mo, ReferenceSeed.ToFederalRules(c.TaxYear), ReferenceSeed.ToMissouriRules(c.TaxYear));
    }

    /// <summary>Every check in the year up to and including <paramref name="through"/>, with YTD FICA threaded, using what was in effect on each date.</summary>
    public async Task<List<(DateOnly Date, Context Context, PaycheckResult Result)>> YearToDateAsync(int sourceId, int year, DateOnly through, WhatIfRequest? whatIf = null)
    {
        var source = await db.IncomeSources.Include(s => s.PaySchedules).FirstAsync(s => s.Id == sourceId);
        if (source.EndDate is { } ended && ended < through) through = ended;
        var dates = PayDates.Generate(source.PaySchedules, new DateOnly(year, 1, 1), through);
        var contexts = new List<(DateOnly, Context)>();
        foreach (var d in dates) contexts.Add((d, await LoadAsync(sourceId, d)));
        var results = PaycheckEngine.ComputeYear(contexts.Select(x => (x.Item1, BuildInput(x.Item2, whatIf))));
        return contexts.Zip(results, (c, r) => (c.Item1, c.Item2, r.Result)).ToList();
    }

    public async Task<PaycheckEstimateDto> EstimateAsync(int sourceId, DateOnly date, WhatIfRequest? whatIf = null)
    {
        var c = await LoadAsync(sourceId, date);
        // YTD FICA wages come from the checks earlier in the year.
        var earlier = await YearToDateAsync(sourceId, date.Year, date.AddDays(-1), whatIf);
        var input = BuildInput(c, whatIf) with
        {
            YtdSocialSecurityWages = earlier.Sum(e => e.Result.FicaWages),
            YtdMedicareWages = earlier.Sum(e => e.Result.FicaWages),
        };
        return ToDto(c, whatIf?.AnnualSalary ?? c.Salary.AnnualAmount, PaycheckEngine.Compute(input));
    }

    public async Task<YearEstimateDto> YearAsync(int sourceId, int year, WhatIfRequest? whatIf = null)
    {
        var runs = await YearToDateAsync(sourceId, year, new DateOnly(year, 12, 31), whatIf);
        var checks = runs.Select(r => new YearCheckDto(r.Date, r.Result.Gross, r.Result.TotalPreTaxDeductions, r.Result.FederalIncomeTax.Amount,
            r.Result.SocialSecurity.Amount, r.Result.Medicare.Amount, r.Result.MissouriIncomeTax.Amount, r.Result.TotalPostTaxDeductions, r.Result.Net)).ToList();
        var warnings = runs.SelectMany(r => r.Context.Warnings).Distinct().ToList();
        var name = runs.FirstOrDefault().Context?.Source.Name ?? (await db.IncomeSources.FindAsync(sourceId))?.Name ?? "";
        if (runs.Count == 0)
            return new YearEstimateDto(year, name, checks, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "no pay dates in this year", ["No pay dates generated for this year; check the pay schedule."]);

        // Year-end federal view uses the W-4 and tax year in effect on the last check.
        var last = runs[^1].Context;
        var input = BuildInput(last, whatIf);
        var taxable = checks.Count > 0 ? runs.Sum(r => r.Result.FederalTaxableWages) : 0m;
        var withheld = checks.Sum(c => c.Federal);
        var ye = PaycheckEngine.FederalYearEnd(taxable, withheld, input.W4, input.Federal);

        return new YearEstimateDto(year, name, checks,
            checks.Sum(c => c.Gross), checks.Sum(c => c.PreTax), withheld, checks.Sum(c => c.SocialSecurity), checks.Sum(c => c.Medicare),
            checks.Sum(c => c.Missouri), checks.Sum(c => c.PostTax), checks.Sum(c => c.Net),
            ye.Liability, ye.Withheld, ye.RefundOrOwed, ye.Formula, warnings);
    }

    public async Task<PaycheckEstimateDto> SupplementalAsync(SupplementalRequest req)
    {
        var c = await LoadAsync(req.IncomeSourceId, req.PayDate);
        var earlier = await YearToDateAsync(req.IncomeSourceId, req.PayDate.Year, req.PayDate.AddDays(-1));
        var regular = BuildInput(c) with
        {
            YtdSocialSecurityWages = earlier.Sum(e => e.Result.FicaWages),
            YtdMedicareWages = earlier.Sum(e => e.Result.FicaWages),
        };
        var result = req.Method == SupplementalMethod.Flat
            ? PaycheckEngine.Supplemental(req.Amount, req.YtdSupplemental, regular with { Deductions = [] })
            : PaycheckEngine.SupplementalAggregate(req.Amount, regular);
        return ToDto(c, c.Salary.AnnualAmount, result);
    }

    public static PaycheckEstimateDto ToDto(Context c, decimal annualSalary, PaycheckResult r)
    {
        static LineDto L(Line l) => new(l.Name, l.Amount, l.Formula);
        var w = c.W4;
        var w4 = $"{Words(w.FederalStatus)}{(w.MultipleJobs ? ", Step 2 checked" : "")}{(w.DependentCredits > 0 ? $", credits {w.DependentCredits:C0}" : "")}{(w.OtherIncome > 0 ? $", other income {w.OtherIncome:C0}" : "")}{(w.Deductions > 0 ? $", deductions {w.Deductions:C0}" : "")}{(w.ExtraWithholding > 0 ? $", extra {w.ExtraWithholding:C}" : "")}";
        var mo = $"{Words(w.MissouriStatus)}{(w.MissouriExtraWithholding > 0 ? $", extra {w.MissouriExtraWithholding:C}" : "")}";
        return new PaycheckEstimateDto(c.Date, c.Source.Name, c.PeriodsPerYear, annualSalary, c.TaxYear.Year, c.TaxYear.Verified, w4, mo,
            r.Gross, r.PreTaxDeductions.Select(L).ToList(), r.FicaWages, r.FederalTaxableWages,
            L(r.FederalIncomeTax), L(r.SocialSecurity), L(r.Medicare), L(r.MissouriIncomeTax),
            r.PostTaxDeductions.Select(L).ToList(), r.TotalPreTaxDeductions, r.TotalTaxes, r.TotalPostTaxDeductions, r.Net, r.Steps, c.Warnings);
    }

    private static string Words(Enum e) => System.Text.RegularExpressions.Regex.Replace(e.ToString(), "(?<=[a-z0-9])([A-Z])", " $1");
}
