using Microsoft.EntityFrameworkCore;
using MyBudget.Contracts;
using MyBudget.Data;
using MyBudget.Domain;
using MyBudget.Engines.Hsa;
using MyBudget.Engines.Ledger;

namespace MyBudget.Api.Endpoints;

public static class HsaEndpoints
{
    public static RouteGroupBuilder MapHsa(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/hsa");

        g.MapGet("/{year:int}", async (int year, BudgetDbContext db) => ToDto(await Load(db, year) ?? new HsaYear { Year = year }));

        g.MapPut("/{year:int}", async (int year, HsaYearDto dto, BudgetDbContext db) =>
        {
            var y = await Load(db, year) ?? db.HsaYears.Add(new HsaYear { Year = year }).Entity;
            y.TargetAmount = dto.TargetAmount; y.TargetDate = dto.TargetDate; y.CatchUpEligible = dto.CatchUpEligible;
            y.LimitOverrideSelfOnly = dto.LimitOverrideSelfOnly; y.LimitOverrideFamily = dto.LimitOverrideFamily; y.Notes = dto.Notes;
            y.Months.Clear();
            y.Months.AddRange(Enumerable.Range(1, 12).Select(m => new HsaMonth { Month = m, Tier = m - 1 < dto.Months.Count ? dto.Months[m - 1] : HsaTier.NotEligible }));
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(y));
        });

        g.MapPost("/{year:int}/contributions", async (int year, HsaContributionDto dto, BudgetDbContext db) =>
        {
            var y = await Load(db, year) ?? db.HsaYears.Add(new HsaYear { Year = year }).Entity;
            var c = new HsaContribution { Date = dto.Date, Amount = dto.Amount, Source = dto.Source, AccountId = dto.AccountId, Notes = dto.Notes };
            y.Contributions.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(y));
        });

        g.MapDelete("/{year:int}/contributions/{id:int}", async (int year, int id, BudgetDbContext db) =>
        {
            var y = await Load(db, year);
            var c = y?.Contributions.FirstOrDefault(x => x.Id == id);
            if (c is null) return Results.NotFound();
            y!.Contributions.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/{year:int}/plan", async (int year, DateOnly? asOf, BudgetDbContext db, TimeProvider clock) =>
        {
            var today = asOf ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var y = await Load(db, year) ?? new HsaYear { Year = year };
            var limits = await db.ContributionLimits.FirstOrDefaultAsync(l => l.Year == year);
            var (self, family, catchUp, source) = limits is null
                ? (0m, 0m, 0m, $"no {year} limits loaded; enter them under Tax tables")
                : (y.LimitOverrideSelfOnly ?? limits.HsaSelfOnly, y.LimitOverrideFamily ?? limits.HsaFamily, limits.HsaCatchUp,
                   (y.LimitOverrideSelfOnly ?? y.LimitOverrideFamily) is not null ? "your override" : $"{year} limits table{(limits.Verified ? "" : " (unverified)")}");

            var targetDate = y.TargetDate ?? new DateOnly(year, 12, 31);
            var (paychecksLeft, paySource) = await PaychecksBetween(db, today, targetDate);
            var plan = HsaPlanner.Plan(new HsaPlanInput(year,
                Enumerable.Range(1, 12).Select(m => y.Months.FirstOrDefault(x => x.Month == m)?.Tier ?? HsaTier.NotEligible).ToList(),
                new HsaLimits(self, family, catchUp),
                y.Contributions.Select(c => new HsaContributionInput(c.Date, c.Amount, c.Source)).ToList(),
                today, paychecksLeft, y.CatchUpEligible, y.TargetAmount, y.TargetDate));

            return new HsaPlanDto(year, today, self, family, catchUp, source, plan.EligibleMonths, plan.AnnualLimit, plan.LimitFormula,
                plan.Employer, plan.Payroll, plan.Direct, plan.Contributed, plan.Room, plan.Target, plan.TargetSource, plan.RemainingToTarget,
                plan.MonthsLeft, plan.RecommendedMonthly, plan.PaychecksLeft, plan.RecommendedPerPaycheck, paySource, plan.OverContributed, plan.OverBy, plan.Steps);
        });

        return api;
    }

    private static Task<HsaYear?> Load(BudgetDbContext db, int year)
        => db.HsaYears.Include(y => y.Months).Include(y => y.Contributions).FirstOrDefaultAsync(y => y.Year == year);

    private static async Task<(int Count, string Source)> PaychecksBetween(BudgetDbContext db, DateOnly from, DateOnly to)
    {
        var w2 = await db.IncomeSources.Include(s => s.PaySchedules).Where(s => s.IsActive && s.Type == IncomeSourceType.W2Salary).ToListAsync();
        var dates = w2.SelectMany(s => PayDates.Generate(s.PaySchedules, from, to)).Distinct().Count();
        return dates > 0 ? (dates, $"{dates} W-2 pay dates between {from:MMM d} and {to:MMM d, yyyy}") : (0, "no W-2 pay dates in the window");
    }

    private static HsaYearDto ToDto(HsaYear y) => new()
    {
        Year = y.Year, TargetAmount = y.TargetAmount, TargetDate = y.TargetDate, CatchUpEligible = y.CatchUpEligible,
        LimitOverrideSelfOnly = y.LimitOverrideSelfOnly, LimitOverrideFamily = y.LimitOverrideFamily, Notes = y.Notes,
        Months = Enumerable.Range(1, 12).Select(m => y.Months.FirstOrDefault(x => x.Month == m)?.Tier ?? HsaTier.NotEligible).ToList(),
        Contributions = y.Contributions.OrderBy(c => c.Date).Select(c => new HsaContributionDto { Id = c.Id, Date = c.Date, Amount = c.Amount, Source = c.Source, AccountId = c.AccountId, Notes = c.Notes }).ToList(),
    };
}
