using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;

namespace MyBudget.Api.Endpoints;

/// <summary>Year-keyed tax rules and contribution limits: view, edit, mark verified. Percent in the DTOs, fractions in storage.</summary>
public static class ReferenceEndpoints
{
    public static RouteGroupBuilder MapReference(this RouteGroupBuilder api)
    {
        api.MapGet("/reference-years", async (BudgetDbContext db) =>
        {
            var tax = await db.TaxYears.ToDictionaryAsync(t => t.Year);
            var lim = await db.ContributionLimits.ToDictionaryAsync(l => l.Year);
            return tax.Keys.Union(lim.Keys).OrderByDescending(y => y)
                .Select(y => new ReferenceYearSummaryDto(y, tax.GetValueOrDefault(y)?.Verified ?? false, lim.GetValueOrDefault(y)?.Verified ?? false, tax.GetValueOrDefault(y)?.Notes));
        });

        var t = api.MapGroup("/tax-tables");
        t.MapGet("/{year:int}", async (int year, BudgetDbContext db) =>
            await db.TaxYears.Include(x => x.Brackets).FirstOrDefaultAsync(x => x.Year == year) is { } ty ? Results.Ok(ToDto(ty)) : Results.NotFound());

        t.MapPut("/{year:int}", async (int year, TaxYearDto dto, BudgetDbContext db) =>
        {
            var ty = await db.TaxYears.Include(x => x.Brackets).FirstOrDefaultAsync(x => x.Year == year) ?? db.TaxYears.Add(new TaxYear { Year = year }).Entity;
            ty.FederalStandardDeductionSingle = dto.FederalStandardDeductionSingle; ty.FederalStandardDeductionMarriedJointly = dto.FederalStandardDeductionMarriedJointly;
            ty.FederalStandardDeductionHeadOfHousehold = dto.FederalStandardDeductionHeadOfHousehold;
            ty.SocialSecurityRate = dto.SocialSecurityRatePercent / 100m; ty.SocialSecurityWageBase = dto.SocialSecurityWageBase;
            ty.MedicareRate = dto.MedicareRatePercent / 100m; ty.AdditionalMedicareRate = dto.AdditionalMedicareRatePercent / 100m; ty.AdditionalMedicareThreshold = dto.AdditionalMedicareThreshold;
            ty.SupplementalRate = dto.SupplementalRatePercent / 100m; ty.SupplementalHighRate = dto.SupplementalHighRatePercent / 100m; ty.SupplementalHighThreshold = dto.SupplementalHighThreshold;
            ty.MissouriStandardDeductionSingle = dto.MissouriStandardDeductionSingle; ty.MissouriStandardDeductionMarriedSpouseWorks = dto.MissouriStandardDeductionMarriedSpouseWorks;
            ty.MissouriStandardDeductionMarriedOneIncome = dto.MissouriStandardDeductionMarriedOneIncome; ty.MissouriStandardDeductionHeadOfHousehold = dto.MissouriStandardDeductionHeadOfHousehold;
            ty.MissouriSupplementalRate = dto.MissouriSupplementalRatePercent / 100m;
            ty.LtcgThreshold15Single = dto.LtcgThreshold15Single; ty.LtcgThreshold15MarriedJointly = dto.LtcgThreshold15MarriedJointly; ty.LtcgThreshold15HeadOfHousehold = dto.LtcgThreshold15HeadOfHousehold;
            ty.LtcgThreshold20Single = dto.LtcgThreshold20Single; ty.LtcgThreshold20MarriedJointly = dto.LtcgThreshold20MarriedJointly; ty.LtcgThreshold20HeadOfHousehold = dto.LtcgThreshold20HeadOfHousehold;
            ty.Source = dto.Source; ty.Verified = dto.Verified; ty.Notes = dto.Notes;
            ty.Brackets.Clear();
            ty.Brackets.AddRange(dto.Brackets.Select(b => new TaxBracket { Jurisdiction = b.Jurisdiction, FilingStatus = b.Jurisdiction == Jurisdiction.Federal ? b.FilingStatus : null, Over = b.Over, Rate = b.RatePercent / 100m }));
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(ty));
        });

        // Start a new year from the previous one (or the built-in set if the engine knows it).
        t.MapPost("/{year:int}/copy-from/{fromYear:int}", async (int year, int fromYear, BudgetDbContext db) =>
        {
            if (await db.TaxYears.AnyAsync(x => x.Year == year)) return Results.Conflict($"{year} already exists.");
            var from = await db.TaxYears.Include(x => x.Brackets).FirstOrDefaultAsync(x => x.Year == fromYear);
            if (from is null) return Results.NotFound();
            var copy = ReferenceSeedCopy(from, year);
            db.TaxYears.Add(copy);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(copy));
        });

        var l = api.MapGroup("/limits");
        l.MapGet("/{year:int}", async (int year, BudgetDbContext db) =>
            await db.ContributionLimits.FirstOrDefaultAsync(x => x.Year == year) is { } cl ? Results.Ok(ToDto(cl)) : Results.NotFound());
        l.MapPut("/{year:int}", async (int year, ContributionLimitsDto dto, BudgetDbContext db) =>
        {
            var cl = await db.ContributionLimits.FirstOrDefaultAsync(x => x.Year == year) ?? db.ContributionLimits.Add(new ContributionLimits { Year = year }).Entity;
            cl.HsaSelfOnly = dto.HsaSelfOnly; cl.HsaFamily = dto.HsaFamily; cl.HsaCatchUp = dto.HsaCatchUp;
            cl.Retirement401kEmployee = dto.Retirement401kEmployee; cl.Retirement401kCatchUp = dto.Retirement401kCatchUp; cl.Retirement401kTotal = dto.Retirement401kTotal;
            cl.Ira = dto.Ira; cl.IraCatchUp = dto.IraCatchUp; cl.Source = dto.Source; cl.Verified = dto.Verified;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(cl));
        });

        return api;
    }

    private static TaxYear ReferenceSeedCopy(TaxYear from, int year) => new()
    {
        Year = year,
        FederalStandardDeductionSingle = from.FederalStandardDeductionSingle, FederalStandardDeductionMarriedJointly = from.FederalStandardDeductionMarriedJointly, FederalStandardDeductionHeadOfHousehold = from.FederalStandardDeductionHeadOfHousehold,
        SocialSecurityRate = from.SocialSecurityRate, SocialSecurityWageBase = from.SocialSecurityWageBase, MedicareRate = from.MedicareRate, AdditionalMedicareRate = from.AdditionalMedicareRate, AdditionalMedicareThreshold = from.AdditionalMedicareThreshold,
        SupplementalRate = from.SupplementalRate, SupplementalHighRate = from.SupplementalHighRate, SupplementalHighThreshold = from.SupplementalHighThreshold,
        MissouriStandardDeductionSingle = from.MissouriStandardDeductionSingle, MissouriStandardDeductionMarriedSpouseWorks = from.MissouriStandardDeductionMarriedSpouseWorks, MissouriStandardDeductionMarriedOneIncome = from.MissouriStandardDeductionMarriedOneIncome, MissouriStandardDeductionHeadOfHousehold = from.MissouriStandardDeductionHeadOfHousehold,
        MissouriSupplementalRate = from.MissouriSupplementalRate,
        Source = $"Copied from {from.Year}; update every figure", Verified = false, Notes = $"Copied from {from.Year} on {DateTime.Today:yyyy-MM-dd}; not yet updated.",
        Brackets = from.Brackets.Select(b => new TaxBracket { Jurisdiction = b.Jurisdiction, FilingStatus = b.FilingStatus, Over = b.Over, Rate = b.Rate }).ToList(),
    };

    public static TaxYearDto ToDto(TaxYear t) => new()
    {
        Year = t.Year,
        FederalStandardDeductionSingle = t.FederalStandardDeductionSingle, FederalStandardDeductionMarriedJointly = t.FederalStandardDeductionMarriedJointly, FederalStandardDeductionHeadOfHousehold = t.FederalStandardDeductionHeadOfHousehold,
        SocialSecurityRatePercent = t.SocialSecurityRate * 100m, SocialSecurityWageBase = t.SocialSecurityWageBase,
        MedicareRatePercent = t.MedicareRate * 100m, AdditionalMedicareRatePercent = t.AdditionalMedicareRate * 100m, AdditionalMedicareThreshold = t.AdditionalMedicareThreshold,
        SupplementalRatePercent = t.SupplementalRate * 100m, SupplementalHighRatePercent = t.SupplementalHighRate * 100m, SupplementalHighThreshold = t.SupplementalHighThreshold,
        MissouriStandardDeductionSingle = t.MissouriStandardDeductionSingle, MissouriStandardDeductionMarriedSpouseWorks = t.MissouriStandardDeductionMarriedSpouseWorks,
        MissouriStandardDeductionMarriedOneIncome = t.MissouriStandardDeductionMarriedOneIncome, MissouriStandardDeductionHeadOfHousehold = t.MissouriStandardDeductionHeadOfHousehold,
        MissouriSupplementalRatePercent = t.MissouriSupplementalRate * 100m,
        LtcgThreshold15Single = t.LtcgThreshold15Single, LtcgThreshold15MarriedJointly = t.LtcgThreshold15MarriedJointly, LtcgThreshold15HeadOfHousehold = t.LtcgThreshold15HeadOfHousehold,
        LtcgThreshold20Single = t.LtcgThreshold20Single, LtcgThreshold20MarriedJointly = t.LtcgThreshold20MarriedJointly, LtcgThreshold20HeadOfHousehold = t.LtcgThreshold20HeadOfHousehold,
        Source = t.Source, Verified = t.Verified, Notes = t.Notes,
        Brackets = t.Brackets.OrderBy(b => b.Jurisdiction).ThenBy(b => b.FilingStatus).ThenBy(b => b.Over)
            .Select(b => new TaxBracketDto { Jurisdiction = b.Jurisdiction, FilingStatus = b.FilingStatus, Over = b.Over, RatePercent = b.Rate * 100m }).ToList(),
    };

    public static ContributionLimitsDto ToDto(ContributionLimits l) => new()
    {
        Year = l.Year, HsaSelfOnly = l.HsaSelfOnly, HsaFamily = l.HsaFamily, HsaCatchUp = l.HsaCatchUp,
        Retirement401kEmployee = l.Retirement401kEmployee, Retirement401kCatchUp = l.Retirement401kCatchUp, Retirement401kTotal = l.Retirement401kTotal,
        Ira = l.Ira, IraCatchUp = l.IraCatchUp, Source = l.Source, Verified = l.Verified,
    };
}
