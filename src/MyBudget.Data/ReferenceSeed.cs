using Microsoft.EntityFrameworkCore;
using MyBudget.Domain;
using MyBudget.Engines.Paycheck;

namespace MyBudget.Data;

/// <summary>
/// Year-keyed reference tables (tax rules, contribution limits). Runs on every start, for every
/// provider, and only inserts years that are missing, so edits made in the app survive restarts.
/// </summary>
public static class ReferenceSeed
{
    public static async Task SeedAsync(BudgetDbContext db, CancellationToken ct = default)
    {
        foreach (var year in new[] { 2025, 2026 })
        {
            if (!await db.TaxYears.AnyAsync(t => t.Year == year, ct))
                db.TaxYears.Add(BuildTaxYear(year));
            if (!await db.ContributionLimits.AnyAsync(l => l.Year == year, ct))
                db.ContributionLimits.Add(BuildLimits(year));
        }
        await db.SaveChangesAsync(ct);
    }

    public static TaxYear BuildTaxYear(int year)
    {
        var f = RuleSets.Federal(year);
        var m = RuleSets.Missouri(year);
        var provisional = RuleSets.IsProvisional(year, Jurisdiction.Missouri);
        var t = new TaxYear
        {
            Year = year,
            FederalStandardDeductionSingle = f.StandardDeduction[FederalFilingStatus.SingleOrMarriedFilingSeparately],
            FederalStandardDeductionMarriedJointly = f.StandardDeduction[FederalFilingStatus.MarriedFilingJointly],
            FederalStandardDeductionHeadOfHousehold = f.StandardDeduction[FederalFilingStatus.HeadOfHousehold],
            SocialSecurityRate = f.SocialSecurityRate, SocialSecurityWageBase = f.SocialSecurityWageBase,
            MedicareRate = f.MedicareRate, AdditionalMedicareRate = f.AdditionalMedicareRate, AdditionalMedicareThreshold = f.AdditionalMedicareThreshold,
            SupplementalRate = f.SupplementalRate, SupplementalHighRate = f.SupplementalHighRate, SupplementalHighThreshold = f.SupplementalHighThreshold,
            MissouriStandardDeductionSingle = m.StandardDeduction[MissouriFilingStatus.Single],
            MissouriStandardDeductionMarriedSpouseWorks = m.StandardDeduction[MissouriFilingStatus.MarriedSpouseWorks],
            MissouriStandardDeductionMarriedOneIncome = m.StandardDeduction[MissouriFilingStatus.MarriedOneIncome],
            MissouriStandardDeductionHeadOfHousehold = m.StandardDeduction[MissouriFilingStatus.HeadOfHousehold],
            MissouriSupplementalRate = m.SupplementalRate,
            Source = $"Built-in: IRS Rev. Proc. brackets + Pub 15-T {year}; SSA wage base {year}; Missouri Employer's Tax Guide",
            Verified = false,
            Notes = provisional ? "Missouri 2026 bracket edges and top rate are carried from 2025 until confirmed against the 2026 MO withholding formula." : null,
        };
        foreach (var (status, brackets) in f.Brackets)
            t.Brackets.AddRange(brackets.Select(b => new TaxBracket { Jurisdiction = Jurisdiction.Federal, FilingStatus = status, Over = b.Over, Rate = b.Rate }));
        t.Brackets.AddRange(m.Brackets.Select(b => new TaxBracket { Jurisdiction = Jurisdiction.Missouri, Over = b.Over, Rate = b.Rate }));
        return t;
    }

    public static ContributionLimits BuildLimits(int year) => year switch
    {
        2025 => new() { Year = 2025, HsaSelfOnly = 4_300m, HsaFamily = 8_550m, HsaCatchUp = 1_000m, Retirement401kEmployee = 23_500m, Retirement401kCatchUp = 7_500m, Retirement401kTotal = 70_000m, Ira = 7_000m, IraCatchUp = 1_000m, Source = "IRS Rev. Proc. 2024-25 (HSA), Notice 2024-80 (401k/IRA)" },
        2026 => new() { Year = 2026, HsaSelfOnly = 4_400m, HsaFamily = 8_750m, HsaCatchUp = 1_000m, Retirement401kEmployee = 24_500m, Retirement401kCatchUp = 8_000m, Retirement401kTotal = 72_000m, Ira = 7_500m, IraCatchUp = 1_100m, Source = "IRS Rev. Proc. 2025-19 (HSA), Notice 2025-67 (401k/IRA)" },
        _ => throw new ArgumentOutOfRangeException(nameof(year)),
    };

    /// <summary>Turns the stored rows back into engine rule sets.</summary>
    public static FederalRules ToFederalRules(TaxYear t) => new(t.Year,
        new Dictionary<FederalFilingStatus, decimal>
        {
            [FederalFilingStatus.SingleOrMarriedFilingSeparately] = t.FederalStandardDeductionSingle,
            [FederalFilingStatus.MarriedFilingJointly] = t.FederalStandardDeductionMarriedJointly,
            [FederalFilingStatus.HeadOfHousehold] = t.FederalStandardDeductionHeadOfHousehold,
        },
        Enum.GetValues<FederalFilingStatus>().ToDictionary(s => s, s => (IReadOnlyList<Bracket>)t.Brackets
            .Where(b => b.Jurisdiction == Jurisdiction.Federal && b.FilingStatus == s).OrderBy(b => b.Over).Select(b => new Bracket(b.Over, b.Rate)).ToList()),
        t.SocialSecurityRate, t.SocialSecurityWageBase, t.MedicareRate, t.AdditionalMedicareRate, t.AdditionalMedicareThreshold,
        t.SupplementalRate, t.SupplementalHighRate, t.SupplementalHighThreshold);

    public static MissouriRules ToMissouriRules(TaxYear t) => new(t.Year,
        new Dictionary<MissouriFilingStatus, decimal>
        {
            [MissouriFilingStatus.Single] = t.MissouriStandardDeductionSingle,
            [MissouriFilingStatus.MarriedSpouseWorks] = t.MissouriStandardDeductionMarriedSpouseWorks,
            [MissouriFilingStatus.MarriedOneIncome] = t.MissouriStandardDeductionMarriedOneIncome,
            [MissouriFilingStatus.HeadOfHousehold] = t.MissouriStandardDeductionHeadOfHousehold,
        },
        t.Brackets.Where(b => b.Jurisdiction == Jurisdiction.Missouri).OrderBy(b => b.Over).Select(b => new Bracket(b.Over, b.Rate)).ToList(),
        t.MissouriSupplementalRate);
}
